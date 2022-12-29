using AngryWasp.Logger;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public static class CommandProcessor
    {
        private static Dictionary<byte, Func<Connection, Header, byte[], Task>> commands = new Dictionary<byte, Func<Connection, Header, byte[], Task>>();

        public static void RegisterDefaultCommands()
        {
            RegisterCommand("Ping", Ping.CODE, Ping.GenerateResponse);
            RegisterCommand("ExchangePeerList", ExchangePeerList.CODE, ExchangePeerList.GenerateResponse);
        }

        public static void RegisterCommand(string name, byte cmd, Func<Connection, Header, byte[], Task> handler)
        {
            if (commands.ContainsKey(cmd))
            {
                Log.Instance.WriteWarning($"Attempt to add duplicate command handler for byte code {cmd}");
                return;
            }

            //Log.Instance.WriteInfo($"Added command handler for byte code {name}");
            commands.Add(cmd, handler);
        }

        public static void Process(Connection c, Header h, byte[] d)
        {
            if (h.DataLength != d.Length)
            {
                Log.Instance.WriteWarning($"Received incomplete message. Expected {h.DataLength} bytes, got {d.Length}");
                return;
            }

            Task.Run(async () =>
           {
               await commands[h.Command].Invoke(c, h, d).ConfigureAwait(false);
           });
        }
    }
}
