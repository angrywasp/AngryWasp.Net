using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public class Ping
    {
        public const byte CODE = 3;

        public static byte[] GenerateRequest() => Header.Create(CODE).ToArray();

        public static async Task GenerateResponse(Connection c, Header h, byte[] d)
        {
            if (!h.IsRequest)
                return;

            var writeOk = await c.WriteAsync(Header.Create(CODE, false).ToArray()).ConfigureAwait(false);

            if (!writeOk)
                await ConnectionManager.RemoveAsync(c, $"Could not write to connection {c.PeerId}");
        }
    }
}
