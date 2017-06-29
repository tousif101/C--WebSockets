    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;

    public class ChatWebSocket
    {
        public const int maxBufferSize = 4096;

        WebSocket websocket;

        /**
        Dictionary to keep track of all the sockets
         */
        public static ConcurrentDictionary<string,WebSocket> socketMap = new ConcurrentDictionary<string, WebSocket>(); 

        ChatWebSocket(WebSocket socket)
        {
            this.websocket = socket;
        }

        /**
        Start the socket, and assign a new uuid to the socket.
        Add to map
         */
        static async Task runSocket(HttpContext hc, Func<Task> n)
        {
            if (!hc.WebSockets.IsWebSocketRequest)
                return;

            var socket = await hc.WebSockets.AcceptWebSocketAsync();
        
            var id = Guid.NewGuid().ToString();
            socketMap.TryAdd(id, socket);

            for(;;)
            {
                if (socket.State != WebSocketState.Open) break;
                if(CancellationToken.None.IsCancellationRequested)
                {
                    break;
                }
                //recive message here
                var res = await recieveMessage(socket,CancellationToken.None);
                if(res.Length == 0){
                    if(socket.State != WebSocketState.Open)
                    {
                        break;
                    }
                    continue;
                }
                
                pingClient(socket,res,CancellationToken.None);
                //Send the message back
            }
            WebSocket test;
            socketMap.TryRemove(id,out test);

            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure,"Closed", CancellationToken.None);
            socket.Dispose();
            //Close out all websockets from the dictionary
        }

        /**
        function to send data to the client
         */
        static async void pingClient(WebSocket sockets,string data, CancellationToken cToken)
        {
            foreach (var socket in socketMap)
            {
                if(socket.Value.State != WebSocketState.Open)
                {
                    continue;
                }
                
                var bytes = Encoding.UTF8.GetBytes(data);
                var seg = new ArraySegment<byte>(bytes);
                await socket.Value.SendAsync(seg,WebSocketMessageType.Text, true, cToken);
            }
        }

        /**
        function to recieve the messages
        */
        static async Task<string> recieveMessage(WebSocket socket,CancellationToken cToken)
        {
            
            var byteArray = new ArraySegment<byte>(new byte[8192]);
            var memStream = new MemoryStream();

            WebSocketReceiveResult result;
            cToken.ThrowIfCancellationRequested();
            result = await socket.ReceiveAsync(byteArray,cToken);
            memStream.Write(byteArray.Array,byteArray.Offset,result.Count);

            while(!result.EndOfMessage){
                cToken.ThrowIfCancellationRequested();
                result = await socket.ReceiveAsync(byteArray,cToken);
                memStream.Write(byteArray.Array,byteArray.Offset,result.Count);
            }
            memStream.Seek(0,SeekOrigin.Begin);

            if(result.MessageType !=WebSocketMessageType.Text){
                return null;
            }
            var read = new StreamReader(memStream,Encoding.UTF8);
            
            return await read.ReadToEndAsync(); 
        }
        public static void Map(IApplicationBuilder app)
        {
            app.UseWebSockets();
            app.Use(ChatWebSocket.runSocket);
        }

    }