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
                    NetworkStream ns = client.GetStream();

                    var buffer = new Memory<byte>(new byte[1024]);
                    int bytesRead = await ns.ReadAsync(buffer).ConfigureAwait(false);
                    var data = buffer.Slice(0, bytesRead).ToArray();

                    var header = Header.Parse(data);

                    if (header == null)
                    {
                        client.Close();
                        return;
                    }

                    bool accept = true;

                    if (header.Command != Handshake.CODE)
                        accept = false;
                    else if (data.Length != Header.LENGTH + header.DataLength)
                        accept = false;
                    else if (Server.PeerId == header.PeerID)
                        accept = false;

                    if (!accept)
                    {
                        client.Close();
                        return;
                    }

                    var hasConnection = await ConnectionManager.HasConnection(header.PeerID).ConfigureAwait(false);
                    if (hasConnection)
                    {
                        //we already have this connection, but the person sending the request may not have it registered in
                        //their connection manager which is why we are getting the request. So we send a response to say we
                        //have accepted the request to get them to shut up about it
                        await ns.WriteAsync(Handshake.GenerateRequest(false)).ConfigureAwait(false);
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