using AngryWasp.Math;

namespace AngryWasp.Net
{
    public class Handshake
    {
        public const byte CODE = 1;

        public static byte[] GenerateRequest(bool isRequest)
        {
            var request = Header.Create(CODE, isRequest, ConnectionId.LENGTH + 2);
            request.AddRange(Server.PeerId);
            request.AddRange(Server.Port.ToByte());
            return request.ToArray();
        }
    }
}