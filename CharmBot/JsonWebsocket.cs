﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CharmBot
{
    public class JsonWebsocket
    {
        private static string uuid = Guid.NewGuid().ToString();
        private Uri uri;
        public event Action<JObject> RecievedMessage;
        private CancellationTokenSource cancellationToken;
        private CancellationTokenSource cancellationRecieveToken;
        private ClientWebSocket socket;
        public JsonWebsocket(Uri uri)
        {
            this.uri = uri;
        }

        public async Task Connect()
        {
            cancellationToken = new CancellationTokenSource();
            cancellationRecieveToken = new CancellationTokenSource();
            while (true)
            {
                using (socket = new ClientWebSocket())
                {
                    try
                    {
                        await socket.ConnectAsync(uri, cancellationToken.Token);

                        JObject jObject = new JObject
                        {
                            ["type"] = "connect"
                        };
                        await Send(jObject);
                        await receive(socket);

                    }
                    catch (Exception)
                    {
                        socket = null;
                    }
                }
                await Task.Delay(1000);
            }
        }

        public void Disconnect()
        {
            cancellationToken?.Cancel();
            cancellationRecieveToken?.Cancel();
            socket?.Dispose();
            cancellationToken = null;
            cancellationRecieveToken = null;
            socket = null;
        }

        public async Task Send(JObject data)
        {
            data["uuid"] = uuid;
            if (socket != null && cancellationToken != null)
            {
                await socket.SendAsync(Encoding.UTF8.GetBytes(data.ToString(Formatting.None)), WebSocketMessageType.Text, true, cancellationToken.Token);
            }
        }
        private async Task receive(ClientWebSocket socket)
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, cancellationRecieveToken.Token);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cancellationToken.Token);
                        Disconnect();
                        break;
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                    {
                        RecievedMessage?.Invoke(JObject.Parse(reader.ReadToEnd()));
                    }
                }
            } while (true);
        }
    }
}
