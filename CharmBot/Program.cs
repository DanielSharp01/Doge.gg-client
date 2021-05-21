using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;

namespace CharmBot
{
    class Program
    {
        static HashSet<string> champions = new HashSet<string>();

        static bool isChampion(string name)
        {
            if (name == null) return false;
            return champions.Contains(name.ToLower().Replace(" ", ""));
        }

        static void Main(string[] args)
        {
            JObject championData = JObject.Parse(File.ReadAllText("./champions.json"));
            var championArr = (championData["data"] as JObject).Properties().Select(k => k.Name.Replace(" ", "").ToLower()).ToArray();
            foreach (string champion in championArr)
            {
                champions.Add(champion);
            }
            Thread connectionThread = new Thread(() =>
            {
                TcpClient client = null;
                Stream stream = null;
                Thread thread = null;
                try
                {
                    bool serverRefusedLast = false;
                    while (true)
                    {
                        try
                        {
                            client = new TcpClient("localhost", 5050);
                            stream = client.GetStream();
                            stream.WriteAsync(new byte[] { (byte)'D' }, 0, 1);
                            var buffer = new byte[1];
                            stream.Read(buffer, 0, 1);
                            if (buffer[0] != 'S') throw new Exception("Server refused");
                            serverRefusedLast = false;
                            Console.WriteLine("Server connected");
                            thread = startupThread(stream);
                            while (true)
                            {
                                stream.Write(new byte[] { (byte)'B' }, 0, 1);
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception e) when(!(e is ThreadInterruptedException))
                        {
                            if (!serverRefusedLast)
                            {
                                Console.WriteLine("Server refused");
                            }
                            serverRefusedLast = true;
                            client?.Dispose();
                            thread?.Interrupt();
                            stream?.Close();
                        }
                        Thread.Sleep(1000);
                    }
                }
                catch (ThreadInterruptedException) {
                    stream.WriteAsync(new byte[] { (byte)'E' }, 0, 1);
                    client?.Dispose();
                    thread?.Interrupt();
                    stream?.Close();
                }
            });
            connectionThread.Start();

            Thread startupThread(Stream stream)
            {
                var thread = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            stream.WriteAsync(new byte[] { (byte)'W' }, 0, 1);
                            Console.WriteLine("Waiting for process");
                            var process = Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("league of legends")).FirstOrDefault();
                            while (process == null)
                            {
                                process = Process.GetProcesses().Where(p => p.ProcessName.ToLower().Contains("league of legends")).FirstOrDefault();
                                Thread.Sleep(100);
                            }
                            stream.WriteAsync(new byte[] { (byte)'G' }, 0, 1);
                            Console.WriteLine("Game process found");
                            var reader = new ProcessMemoryReader() { Process = process };
                            reader.Open();
                            try
                            {
                                GameObject localPlayer = new GameObject(reader, reader.ReadInt(reader.ModuleBaseAddr + Offsets.LocalPlayer));
                                while (localPlayer.Name != "Ahri")
                                {
                                    localPlayer = new GameObject(reader, reader.ReadInt(reader.ModuleBaseAddr + Offsets.LocalPlayer));
                                    Thread.Sleep(1000);
                                }
                                Console.WriteLine("Ahri found");
                                stream.WriteAsync(new byte[] { (byte)'A' }, 0, 1);
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
                                            if (!seenCharm) stream.WriteAsync(new byte[] { (byte)'C' }, 0, 1);
                                            stream.WriteAsync(new byte[] { (byte)'H' }, 0, 1);
                                            charmEffectSeenAt = ahriCharmBuff.StartTime;
                                        }
                                    }

                                    if (ahriCharm != null)
                                    {
                                        if (!seenCharm) stream.WriteAsync(new byte[] { (byte)'C' }, 0, 1);
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
                    }
                    catch (ThreadInterruptedException) { }
                })
                { IsBackground = true };
                thread.Start();
                return thread;
            }
            Console.ReadLine();
            connectionThread.Interrupt();
        }
    }
}
