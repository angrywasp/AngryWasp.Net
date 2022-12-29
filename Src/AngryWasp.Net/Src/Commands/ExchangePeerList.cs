using AngryWasp.Math;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public class ExchangePeerList
    {
        public const byte CODE = 2;

        public static async Task<byte[]> GenerateRequest(bool isRequest, List<Node> exclusions)
        {
            List<byte> peers = await ConnectionManager.GetPeerList(exclusions).ConfigureAwait(false);
            var request = Header.Create(CODE, isRequest, (ushort)peers.Count);
            request.AddRange(peers);
            return request.ToArray();
        }

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            //reconstruct the node list sent to us by the client
            int offset = 0;
            int count = 0;
            List<Node> nodes = new List<Node>();

            count = d.Length / (6 + ConnectionId.LENGTH);
            for (int i = 0; i < count; i++)
            {
                var host = $"{d[offset++]}.{d[offset++]}.{d[offset++]}.{d[offset++]}";
                var port = d.ToUShort(ref offset);
                var id = d.Skip(offset).Take(ConnectionId.LENGTH).ToArray();
                offset += ConnectionId.LENGTH;
                nodes.Add(new Node(host, port, id));
            }

            await Client.ConnectToNodeList(nodes).ConfigureAwait(false);

            if (!h.IsRequest)
                return;

            var res = await GenerateRequest(false, nodes).ConfigureAwait(false);
            await c.WriteAsync(res).ConfigureAwait(false);
        }
    }
}