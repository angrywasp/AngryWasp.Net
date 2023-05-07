using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using AngryWasp.Cli;
using AngryWasp.Cli.Args;
using AngryWasp.Cli.DefaultCommands;
using AngryWasp.Logger;

namespace AngryWasp.Net.Test
{
    internal class MainClass
    {
        private static async Task Main(string[] rawArgs)
        {
            Arguments args = Arguments.Parse(rawArgs);
            Log.CreateInstance();
            ApplicationLogWriter.HideInfo = true;
            Log.Instance.SupressConsoleOutput = true;
            Log.Instance.AddWriter("buffer", new ApplicationLogWriter(new List<(ConsoleColor, string)>()));
            Log.Instance.AddWriter("file", new FileLogWriter(args.GetString("log-file", "app.log")));

            string[] seedNodes = args.GetString("seeds", "127.0.0.1:20000;127.0.0.1:30000").Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var seedNode in seedNodes)
            {
                string[] node = seedNode.Split(':', StringSplitOptions.RemoveEmptyEntries);
                string host = node[0];
                ushort port = 10000;
                if (node.Length > 1)
                    ushort.TryParse(node[1], out port);

                AngryWasp.Net.Config.AddSeedNode(host, port);
            }

            CommandProcessor.RegisterDefaultCommands();
            Config.SetNetId(1);

            TimedEventManager.RegisterEvent("ConnectToSeedNodes", new ConnectToSeedNodes().Execute, 10);
            TimedEventManager.RegisterEvent("ExchangePeerLists", new ExchangePeerLists().Execute, 30);
            TimedEventManager.RegisterEvent("PingPeers", new PingPeers().Execute, 5);

            Application.RegisterCommand("fetch_peers", "Ask your peers for more connections", new FetchPeers().Handle);
            Application.RegisterCommand("peers", "Print a list of connected peers", new PrintPeers().Handle);
            Application.RegisterCommand("clear", "Clear the console", new Clear().Handle);
            Application.RegisterCommand("help", "Print the help", new Help().Handle);
            
            var cId = AngryWasp.Random.RandomString.Hex(40);
            ApplicationLogWriter.WriteImmediate($"Using P2P ID: {cId}");
            
            new Server().Start(args.GetUshort("port", 10000).Value, cId);
            Client.ConnectToSeedNodes();

            Application.Start(loggerAlreadyAttached: true);
        }
    }

    public class ConnectToSeedNodes : ITimedEvent
    {
        public async Task Execute()
        {
            var count = await ConnectionManager.Count().ConfigureAwait(false);
            if (count == 0)
                Client.ConnectToSeedNodes();
        }
    }

    public class ExchangePeerLists : ITimedEvent
    {
        public async Task Execute()
        {
            var request = await ExchangePeerList.GenerateRequest(true, null).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request).ConfigureAwait(false);
        }
    }

    public class PingPeers : ITimedEvent
    {
        public async Task Execute() => await MessageSender.BroadcastAsync(AngryWasp.Net.Ping.GenerateRequest().ToArray()).ConfigureAwait(false);    
    }

    public class FetchPeers : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            Application.LogBufferPaused = true;
            Application.UserInputPaused = true;

            ApplicationLogWriter.WriteImmediate("Fetching additional peers");
            var request = await ExchangePeerList.GenerateRequest(true, null).ConfigureAwait(false);
            await MessageSender.BroadcastAsync(request.ToArray()).ConfigureAwait(false);
            
            Application.LogBufferPaused = false;
            Application.UserInputPaused = false;
            return true;
        }
    }

    public class PrintPeers : IApplicationCommand
    {
        public async Task<bool> Handle(string command)
        {
            Application.LogBufferPaused = true;
            Application.UserInputPaused = true;

            await ConnectionManager.ForEach(Direction.Incoming | Direction.Outgoing, (c) =>
            {
                Console.WriteLine($"{c.PeerId} - {c.Address.MapToIPv4()}:{c.Port}");
            }).ConfigureAwait(false);

            Application.LogBufferPaused = false;
            Application.UserInputPaused = false;
            return true;
        }
    }

    public static class MessageSender
    {
        public static async Task BroadcastAsync(byte[] request, Connection from = null)
        {
            List<Connection> disconnected = new List<Connection>();

            await ConnectionManager.ForEach(Direction.Incoming | Direction.Outgoing, async (c) =>
            {
                if (from != null && c.PeerId == from.PeerId)
                    return; //don't return to the sender

                if (!await c.WriteAsync(request).ConfigureAwait(false))
                    disconnected.Add(c);

            }).ConfigureAwait(false);

            foreach (var c in disconnected)
                await ConnectionManager.RemoveAsync(c, "Not responding").ConfigureAwait(false);
        }
    }
}