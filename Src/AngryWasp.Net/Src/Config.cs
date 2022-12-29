using AngryWasp.Logger;
using System.Collections.Generic;

namespace AngryWasp.Net
{
    public class Node
    {
        public string Host { get; private set; }
        public ushort Port { get; private set; }
        public ConnectionId PeerID { get; private set; }

        public Node(string host, ushort port, ConnectionId peerId)
        {
            Host = host;
            Port = port;
            PeerID = peerId;
        }

        public override string ToString()
        {
            return $"{Host}:{Port}";
        }
    }

    public static class Config
    {
        public const int READ_BUFFER_SIZE = ushort.MaxValue;
        public const int FAILURES_BEFORE_BAN = 3;

        private static byte netId = 0;
        private static List<Node> seedNodes = new List<Node>();

        public static byte NetId => netId;

        public static List<Node> SeedNodes => seedNodes;

        public static void SetNetId(byte id)
        {
            netId = id;
        }

        public static void AddSeedNode(string host, ushort port)
        {
            Log.Instance.WriteInfo($"Adding seed node at {host}:{port}");
            seedNodes.Add(new Node(host, port, ConnectionId.Empty));
        }

        public static bool HasSeedNode(string host, ushort port)
        {
            foreach (var s in seedNodes)
                if (s.Host == host && s.Port == port)
                    return true;

            return false;
        }
    }
}