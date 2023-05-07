using AngryWasp.Helpers;
using AngryWasp.Logger;
using AngryWasp.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    [Flags]
    public enum Direction
    {
        Invalid = 0,
        Incoming = 1,
        Outgoing = 2
    }

    public class Connection
    {
        private static readonly SemaphoreSlim writeLock = new SemaphoreSlim(1);

        private TcpClient client;
        private int failureCount = 0;
        private Direction direction = Direction.Invalid;
        private ushort port = 0;
        private ConnectionId peerId = ConnectionId.Empty;
        private List<string> failureReasons = new List<string>();

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public Direction Direction => direction;

        public ushort Port => port;

        public ConnectionId PeerId => peerId;

        public IPAddress Address => ((IPEndPoint)client.Client.RemoteEndPoint).Address;

        public List<string> FailureReasons => failureReasons;

        public Connection(TcpClient client, ConnectionId peerId, ushort port, Direction direction)
        {
            this.client = client;
            this.peerId = peerId;
            this.port = port;
            this.direction = direction;
            StartListening(cancellationTokenSource.Token);
        }

        public async Task AddFailureAsync(string reason)
        {
            ++failureCount;
            failureReasons.Add(reason);
            Log.Instance.WriteWarning($"{peerId}: {reason}");
            if (failureCount >= Config.FAILURES_BEFORE_BAN)
                await ConnectionManager.RemoveAsync(this, "Too many failures").ConfigureAwait(false);
        }

        public async Task<bool> WriteAsync(byte[] input)
        {
            var available = await IsAvailable(CancellationToken.None).ConfigureAwait(false);
            if (!available) return false;

            await writeLock.WaitAsync().ConfigureAwait(false);

            try
            {
                await client.GetStream().WriteAsync(input, 0, input.Length).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
            finally { writeLock.Release(); }
        }

        private async Task<bool> IsAvailable(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                await ConnectionManager.RemoveAsync(this, "Connection terminated").ConfigureAwait(false);
                return false;
            }

            if (client == null)
            {
                await ConnectionManager.RemoveAsync(this, "TcpClient is null").ConfigureAwait(false);
                return false;
            }

            if (client.Client == null)
            {
                await ConnectionManager.RemoveAsync(this, "TcpClient.Socket is null").ConfigureAwait(false);
                return false;
            }

            if (!client.Client.Connected)
            {
                await ConnectionManager.RemoveAsync(this, "TcpClient.Socket is not connected").ConfigureAwait(false);
                return false;
            }

            return true;
        }

        public void StartListening(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                ThreadSafeList<byte> bufferPool = new ThreadSafeList<byte>();

                try
                {
                    while (true)
                    {
                        bool available = await IsAvailable(cancellationToken).ConfigureAwait(false);
                        if (!available) break;

                        if (client.Available == 0)
                        {
                            await Task.Delay(500).ConfigureAwait(false);
                            continue;
                        }

                        var buffer = new byte[client.Available];
                        int bytesRead = await client.GetStream().ReadAsync(buffer).ConfigureAwait(false);

                        if (bytesRead < buffer.Length)
                        {
                            await ConnectionManager.RemoveAsync(this, "End of stream").ConfigureAwait(false);
                            break;
                        }

                        available = await IsAvailable(cancellationToken).ConfigureAwait(false);
                        if (!available) break;

                        await bufferPool.AddRange(buffer).ConfigureAwait(false);

                        int offset = 0;

                        while (true)
                        {
                            int count = await bufferPool.Count().ConfigureAwait(false);
                            if (offset + Header.LENGTH >= count)
                                break;

                            var copy = await bufferPool.Copy().ConfigureAwait(false);

                            bool isStartOfHeader = copy[offset] == Config.NetId && copy[offset + 1] == Header.PROTOCOL_VERSION;

                            if (!isStartOfHeader)
                            {
                                offset++;
                                continue;
                            }

                            var header = Header.Parse(copy.Skip(offset).Take(Header.LENGTH).ToArray());

                            if (header == null)
                            {
                                offset++;
                                continue;
                            }

                            var requiredLength = offset + Header.LENGTH + header.DataLength;
                            if (requiredLength > copy.Count)
                                break;
                            else
                            {
                                offset += Header.LENGTH;
                                var packageBuffer = copy.Skip(offset).Take(header.DataLength).ToArray();
                                CommandProcessor.Process(this, header, packageBuffer);
                                offset += header.DataLength;

                                bufferPool = new ThreadSafeList<byte>(copy.Skip(offset));

                                if (offset >= copy.Count)
                                    break;

                                offset = 0;
                                continue;
                            }
                        }

                        offset = 0;
                    }
                }
                catch (Exception ex)
                {
                    await ConnectionManager.RemoveAsync(this, ex.Message).ConfigureAwait(false);
                }
            }, cancellationToken);
        }

        public void Close()
        {
            if (client == null || client.Client == null || !client.Client.Connected)
                return;

            client.Close();
        }

        public List<byte> GetPeerListBytes()
        {
            IPEndPoint r = (IPEndPoint)client.Client.RemoteEndPoint;

            return new List<byte>()
                .Join(r.Address.MapToIPv4().GetAddressBytes())
                .Join(port.ToByte())
                .Join(peerId.ToByte());
        }

        public override string ToString()
        {
            return client.Client.RemoteEndPoint.ToString();
        }
    }
}
