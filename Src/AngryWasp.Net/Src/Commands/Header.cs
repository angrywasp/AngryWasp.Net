using AngryWasp.Math;
using System.Collections.Generic;
using System.Linq;

namespace AngryWasp.Net
{
    public class Header
    {
        public const int LENGTH = 26;
        public const byte PROTOCOL_VERSION = 0x01;

        private ConnectionId peerId = ConnectionId.Empty;
        private byte command = 0;
        private ushort dataLength = 0;
        private bool isRequest = true;

        public ConnectionId PeerID => peerId;

        public byte Command => command;

        public ushort DataLength => dataLength;

        public bool IsRequest => isRequest;

        public static List<byte> Create(byte command, bool isRequest = true, ushort dataLength = 0)
        {
            List<byte> bin = new List<byte>();
            bin.Add(Config.NetId);
            bin.Add(PROTOCOL_VERSION);
            bin.AddRange(isRequest.ToByte());
            bin.AddRange(Server.PeerId);
            bin.Add(command);
            bin.AddRange(dataLength.ToByte());
            return bin;
        }

        public static Header Parse(byte[] bin)
        {
            if (bin.Length < Header.LENGTH)
                return null;

            int offset = 0;

            if (bin[offset++] != Config.NetId)
                return null;

            if (bin[offset++] != PROTOCOL_VERSION)
                return null;

            var header = new Header();
            header.isRequest = bin.ToBoolean(ref offset);
            header.peerId = bin.Skip(offset).Take(ConnectionId.LENGTH).ToArray();
            offset += ConnectionId.LENGTH;
            header.command = bin[offset++];
            header.dataLength = bin.ToUShort(ref offset);

            return header;
        }
    }
}