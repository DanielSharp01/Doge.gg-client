using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CharmBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var socket = new JsonWebsocket(new Uri("ws://localhost:5050"));
            var gameClient = new GameClient();
#if CHARM_BOT
            await gameClient.GetChampions();
            gameClient.CharmBotCharmCast += async () => await socket?.Send(new JObject
            {
                ["type"] = "charmCast",
            });
            gameClient.CharmBotCharmHit += async () => await socket?.Send(new JObject
            {
                ["type"] = "charmHit",
            });
#endif
            gameClient.ClientConnected += async (players, activePlayerName) => await socket?.Send(new JObject
            {
                ["type"] = "clientConnected",
                ["players"] = new JArray(players),
                ["activePlayerName"] = activePlayerName,
            });
            gameClient.ClientDisconnected += async () => await socket?.Send(new JObject
            {
                ["type"] = "clientDisconnected",
            });
            gameClient.GotEvents += async (events) => await socket?.Send(new JObject
            {
                ["type"] = "gotEvents",
                ["events"] = new JArray(events),
            });
            _ = gameClient.Observe();
            socket.RecievedMessage += obj =>
            {
                if (obj?["type"].ToString() == "connected")
                {
                    gameClient.ReemitRunning();
                }
            };
            _ = socket.Connect();
            Console.ReadLine();
            socket.Disconnect();
        }
    }
}
