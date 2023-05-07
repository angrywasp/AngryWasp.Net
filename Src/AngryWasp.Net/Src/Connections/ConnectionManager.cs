using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Random;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public static class ConnectionManager
    {
        private static readonly XoShiRo128PlusPlus rng = new XoShiRo128PlusPlus();
        private static ThreadSafeDictionary<ConnectionId, Connection> connections = new ThreadSafeDictionary<ConnectionId, Connection>();

        public delegate void ConnectionEventHandler(Connection connection);

        public static event ConnectionEventHandler Added;
        public static event ConnectionEventHandler Removed;

        public static async Task<bool> AddAsync(Connection value)
        {
            bool added = await connections.Add(value.PeerId, value).ConfigureAwait(false);
            if (added)
            {
                Log.Instance.WriteInfo($"{value.Direction.ToString()} connection added: {value.PeerId}");
                Added?.Invoke(value);
            }
            else
                value.Close();

            return added;
        }

        public static async Task<bool> RemoveAsync(Connection c, string reason)
        {
            c.Close();
            bool removed = await connections.Remove(c.PeerId).ConfigureAwait(false);

            if (removed)
            {
                Log.Instance.WriteInfo($"{c.Direction.ToString()} connection removed - {c.PeerId}: {reason ?? "No reason"}");
                foreach (var r in c.FailureReasons)
                    Log.Instance.WriteInfo(r);

                Removed?.Invoke(c);
            }


            return removed;
        }

        public static async Task<bool> HasConnection(ConnectionId peerId) => await connections.ContainsKey(peerId).ConfigureAwait(false);

        public static async Task<HashSet<ConnectionId>> GetIdHashSet() => await connections.CopyKeysToHashSet().ConfigureAwait(false);

        public static async Task<bool> HasConnection(string host)
        {
            var connectionList = await connections.CopyValues().ConfigureAwait(false);
            foreach (var c in connectionList)
            {
                string compare = $"{c.Address.MapToIPv4().ToString()}:{c.Port.ToString()}";
                if (host == compare)
                    return true;
            }

            return false;
        }

        public static async Task<int> Count() => await connections.Count().ConfigureAwait(false);

        public static async Task ForEach(Direction direction, Action<Connection> action)
        {
            var connectionList = await connections.CopyValues().ConfigureAwait(false);
            foreach (var c in connectionList)
                if (direction.HasFlag(c.Direction))
                    action(c);
        }

        public static async Task<List<byte>> GetPeerList(List<Node> exclusions)
        {
            List<byte> bytes = new List<byte>();

            var connectionList = await connections.CopyValues().ConfigureAwait(false);

            if (exclusions != null)
            {
                var idSet = new HashSet<ConnectionId>(exclusions.Select(x => x.PeerID));

                foreach (var c in connectionList)
                {
                    if (idSet.Contains(c.PeerId))
                        continue;

                    bytes.AddRange(c.GetPeerListBytes());
                }
            }
            else
            {
                foreach (var c in connectionList)
                    bytes.AddRange(c.GetPeerListBytes());
            }

            return bytes;
        }

        public static async Task<Connection> GetConnectionAsync(ConnectionId key) =>
            await connections.Get(key).ConfigureAwait(false);

        public static async Task<Connection> GetRandomConnection()
        {
            var connectionList = await connections.CopyValues().ConfigureAwait(false);

            if (connectionList.Count == 0)
                return null;

            int index = rng.Next(0, connectionList.Count);
            return connectionList[index];
        }
    }
}