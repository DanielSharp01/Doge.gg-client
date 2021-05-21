using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;


namespace CharmBot
{
    public class GameClient
    {
        public event Action<JObject[], string> ClientConnected;
        public event Action<JObject[]> GotEvents;
        public event Action ClientDisconnected;
        private int lastEventId = -1;
        private const string LiveClientDataUrl = "https://127.0.0.1:2999/liveclientdata";
        private readonly HttpClient client;
        private readonly List<JObject> events = new List<JObject>();
        private JObject[] players = null;
        private string activePlayerName = null;
        private bool gameRunning = false;
        private async Task<JToken> requestLiveClientData(string endpoint)
        {
            try
            {
                return JToken.Parse(await client.GetStringAsync(LiveClientDataUrl + "/" + endpoint));
            }
            catch (Exception e)
            {
                return null;
            }
        }

        public GameClient()
        {
            client = new HttpClient(new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
            });

#if CHARM_BOT
            CharmBotCharmCast += (time) =>
            {
                var eventObj = new JObject
                {
                    ["EventName"] = "CharmCast",
                    ["CharmerName"] = activePlayerName,
                    ["EventTime"] = time,
                };
                GotEvents?.Invoke(new JObject[] { eventObj });
            };
            CharmBotCharmHit += (time, charmee) =>
            {
                var eventObj = new JObject
                {
                    ["EventName"] = "CharmHit",
                    ["CharmerName"] = activePlayerName,
                    ["CharmeeName"] = players.FirstOrDefault(p => p?["championName"]?.ToString()?.Replace(" ", "") == charmee)?["summonerName"] ?? charmee,
                    ["EventTime"] = time,
                };
                events.Add(eventObj);
                GotEvents?.Invoke(new JObject[] { eventObj });
            };
#endif
        }

        private async Task<JArray> requestLiveDataPlayers()
        {
            return (await requestLiveClientData("playerlist")) as JArray;
        }
        private async Task<string> requestLiveDataActivePlayer()
        {
            return (await requestLiveClientData("activeplayername")).ToString();
        }

        private async Task<JObject[]> requestEvents()
        {
            var res = (await requestLiveClientData($"eventdata?eventID={lastEventId + 1}")) as JObject;
            if (res != null && res["Events"] != null)
            {
                var events = res["Events"] as JArray;
                if (events.Count > 0)
                {
                    lastEventId = (int)(events[events.Count - 1] as JObject)["EventID"];
                }
                return events.Select(e => e as JObject).ToArray();
            }

            return null;
        }

        public void ReemitRunning()
        {
            if (gameRunning)
            {
                ClientConnected?.Invoke(players, activePlayerName);
                GotEvents?.Invoke(events.ToArray());
            }
        }

        public async Task Observe()
        {
            await waitForGame();
            ClientConnected?.Invoke(players, activePlayerName);
#if CHARM_BOT
            if (players.FirstOrDefault(p => p["summonerName"].ToString() == activePlayerName && p["championName"].ToString() == "Ahri") != null)
            {
                charmBotThread = new Thread(StartCharmBot);
                charmBotThread.Start();
            }
#endif
            await processEvents();
#if CHARM_BOT
            charmBotThread?.Interrupt();
#endif
            ClientDisconnected?.Invoke();
        }

        private async Task waitForGame()
        {
            gameRunning = false;
            events.Clear();
            while (true)
            {
                var playerResponse = await requestLiveDataPlayers();
                if ((playerResponse?[0] as JObject)?["summonerName"] != null)
                {
                    players = playerResponse.Select(p => p as JObject).ToArray();
                    activePlayerName = await requestLiveDataActivePlayer();
                    gameRunning = true;
                    return;

                }
                await Task.Delay(1000);
            }
        }

        private async Task processEvents()
        {
            while (gameRunning)
            {
                var requestedEvents = await requestEvents();
                if (requestedEvents == null) break;
                if (requestedEvents.Length > 0) {
                    GotEvents?.Invoke(requestedEvents);
                }
                events.AddRange(requestedEvents);

                await Task.Delay(500);
            }
        }

#if CHARM_BOT
        public event Action<float> CharmBotCharmCast;
        public event Action<float, string> CharmBotCharmHit;

        private HashSet<string> champions;
        private Thread charmBotThread = null;

        public async Task GetChampions()
        {
            champions = new HashSet<string>(await requestAllChampionsForCurrentVersion());
        }

        private bool isChampion(string name)
        {
            if (name == null) return false;
            return champions.Contains(name.ToLower().Replace(" ", ""));
        }

        private async Task<string[]> requestAllChampionsForCurrentVersion()
        {
            try
            {
                var versions = JArray.Parse(await client.GetStringAsync("https://ddragon.leagueoflegends.com/api/versions.json"));
                var championsRes = JObject.Parse(await client.GetStringAsync($"http://ddragon.leagueoflegends.com/cdn/{versions[0].ToString()}/data/en_US/champion.json"));
                return (championsRes["data"] as JObject)?.Properties().Select(p => p.Name.ToLower().Replace(" ", "")).ToArray();
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void StartCharmBot()
        {
            var process = Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("league of legends")).FirstOrDefault();
            var reader = new ProcessMemoryReader() { Process = process };
            reader.Open();
            try
            {
                GameObject localPlayer = new GameObject(reader, reader.ReadInt(reader.ModuleBaseAddr + Offsets.LocalPlayer));
                int objectManagerPointer = reader.ReadInt(reader.ModuleBaseAddr + Offsets.ObjectManager);
                float charmEffectSeenAt = -1;
                var seenCharm = false;
                while (true)
                {
                    process = Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("league of legends")).FirstOrDefault();
                    if (process == null) break;
                    var gameObjects = GameObject.GetGameObjects(reader, objectManagerPointer);
                    var enemyChampions = gameObjects.Where(g => g.Team != localPlayer.Team && isChampion(g.Name) || (g.Name?.ToLower()?.Contains("dummy") ?? false)).ToList();
                    var ahriCharm = gameObjects.Where(c => c.SpellName == "AhriSeduceMissile").FirstOrDefault();
                    var gameTime = reader.ReadFloat(reader.ModuleBaseAddr + Offsets.GameTime);
                    foreach (var champ in enemyChampions)
                    {
                        champ.ReadBuffs();
                        var ahriCharmBuff = champ.ActiveBuffs.FirstOrDefault(b => b.Name == "AhriSeduce" && gameTime < b.EndTime && charmEffectSeenAt < b.StartTime);
                        if (ahriCharmBuff != null)
                        {
                            if (!seenCharm) CharmBotCharmCast?.Invoke(gameTime);
                            CharmBotCharmHit?.Invoke(gameTime, champ.Name);
                            charmEffectSeenAt = ahriCharmBuff.StartTime;
                        }
                    }

                    if (ahriCharm != null)
                    {
                        if (!seenCharm) CharmBotCharmCast?.Invoke(gameTime);
                        seenCharm = true;
                    }
                    else
                    {
                        seenCharm = false;
                    }
                    Thread.Sleep(10);
                }
            }
            finally
            {
                reader.Close();
            }
        }
#endif
    }
}
