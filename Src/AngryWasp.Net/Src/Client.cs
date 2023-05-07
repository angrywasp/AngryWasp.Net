using AngryWasp.Logger;
using AngryWasp.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public class Client
    {
        public void Connect(string host, ushort port)
        {
            Task.Run(async () =>
            {
                TcpClient client = new TcpClient();

                try
                {
                    Log.Instance.WriteInfo($"Sending connection request to {host}:{port}.");
                    client.Connect(host, port);
                    NetworkStream ns = client.GetStream();

                    var request = Handshake.GenerateRequest(true);
                    await ns.WriteAsync(request).ConfigureAwait(false);

                    var buffer = new Memory<byte>(new byte[1024]);
                    Log.Instance.WriteInfo($"Client waiting for handshake from server {host}:{port}.");
                    int bytesRead = await ns.ReadAsync(buffer).ConfigureAwait(false);
                    var data = buffer.Slice(0, bytesRead).ToArray();

                    var header = Header.Parse(data);

                    if (header == null)
                    {
                        Log.Instance.WriteInfo($"Server {host}:{port} sent invalid header.");
                        client.Close();
                        return;
                    }

                    bool accept = true;

                    if (header.Command != Handshake.CODE)
                    {
                        Log.Instance.WriteWarning($"Server {host}:{port} sent unexpected packet.");
                        accept = false;
                    }
                    else if (data.Length != Header.LENGTH + header.DataLength)
                    {
                        Log.Instance.WriteWarning($"Server {host}:{port} sent incomplete handshake packet.");
                        accept = false;
                    }
                    else if (Server.PeerId == header.PeerID)
                    {
                        Log.Instance.WriteWarning("Attempt to connect to self.");
                        accept = false;
                    }

                    if (!accept)
                    {
                        client.Close();
                        return;
                    }

                    var hasConnection = await ConnectionManager.HasConnection(header.PeerID).ConfigureAwait(false);
                    if (hasConnection)
                    {
                        await ns.WriteAsync(Handshake.GenerateRequest(false)).ConfigureAwait(false);
                        return;
                    }

                    int offset = Header.LENGTH;

                    ConnectionId cId = data.Skip(offset).Take(ConnectionId.LENGTH).ToArray();
                    offset += ConnectionId.LENGTH;
                    ushort cPort = data.ToUShort(ref offset);

                    await ConnectionManager.AddAsync(new Connection(client, cId, cPort, Direction.Outgoing)).ConfigureAwait(false);
                }
                catch { client.Close(); }
            });
        }

        public static void ConnectHost(string host, ushort port) =>
            Task.Run(async () =>
                {
                    var hasConnection = await ConnectionManager.HasConnection($"{host}:{port}").ConfigureAwait(false);
                    if (hasConnection)
                        return;

                    new Client().Connect(host, port);
                }
            );

        public static void ConnectToSeedNodes()
        {
            foreach (var n in Config.SeedNodes)
                Task.Run(async () =>
                    {
                        var hasConnection = await ConnectionManager.HasConnection($"{n.Host}:{n.Port}").ConfigureAwait(false);
                        if (hasConnection)
                            return;

                        new Client().Connect(n.Host, n.Port);
                    }
                );
        }

        public static async Task ConnectToNodeList(List<Node> nodes)
        {
            foreach (var n in nodes)
            {
                var hasConnection = await ConnectionManager.HasConnection(n.PeerID).ConfigureAwait(false);
                if (hasConnection)
                    continue;

                if (n.PeerID == Server.PeerId)
                    continue;

                new Client().Connect(n.Host, n.Port);
            }
        }
    }
}