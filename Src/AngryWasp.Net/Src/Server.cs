using AngryWasp.Logger;
using AngryWasp.Math;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public class Server
    {
        TcpListener listener;

        public static ushort Port { get; private set; }
        public static ConnectionId PeerId { get; private set; }

        public void Start(ushort serverPort, ConnectionId peerId)
        {
            Port = serverPort;
            PeerId = peerId;

            if (PeerId == ConnectionId.Empty)
                throw new ArgumentException("Invalid peer ID");

            if (Port == 0)
                throw new ArgumentException("Invalid server port");

            listener = new TcpListener(IPAddress.Any, Port);
            listener.Start();
            Log.Instance.WriteInfo("Local P2P endpoint: " + listener.LocalEndpoint);
            Log.Instance.WriteInfo("P2P Server initialized");

            Task.Run(() =>
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Log.Instance.WriteInfo($"Incoming connection request from {client.Client.RemoteEndPoint.ToString()}.");
                    HandshakeClient(client);
                }
            });
        }

        public void HandshakeClient(TcpClient client)
        {
            Task.Run(async () =>
            {
                try
                {
                    Log.Instance.WriteInfo($"Waiting for handshake from {client.Client.RemoteEndPoint.ToString()}.");
                    NetworkStream ns = client.GetStream();

                    var buffer = new Memory<byte>(new byte[1024]);
                    int bytesRead = await ns.ReadAsync(buffer).ConfigureAwait(false);
                    var data = buffer.Slice(0, bytesRead).ToArray();

                    var header = Header.Parse(data);

                    if (header == null)
                    {
                        Log.Instance.WriteWarning("Client sent invalid header.");
                        client.Close();
                        return;
                    }

                    bool accept = true;

                    var hasConnection = await ConnectionManager.HasConnection(header.PeerID).ConfigureAwait(false);

                    if (hasConnection)
                        accept = false;
                    if (header.Command != Handshake.CODE)
                    {
                        Log.Instance.WriteWarning("Client sent unexpected packet.");
                        accept = false;
                    }
                    else if (data.Length != Header.LENGTH + header.DataLength)
                    {
                        Log.Instance.WriteWarning("Client sent incomplete handshake packet.");
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

                    int offset = Header.LENGTH;

                    ConnectionId cId = data.Skip(offset).Take(ConnectionId.LENGTH).ToArray();
                    offset += ConnectionId.LENGTH;
                    ushort cPort = data.ToUShort(ref offset);

                    await ns.WriteAsync(Handshake.GenerateRequest(false)).ConfigureAwait(false);
                    await ConnectionManager.AddAsync(new Connection(client, cId, cPort, Direction.Incoming)).ConfigureAwait(false);
                }
                catch { client.Close(); }
            });
        }
    }
}