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

            await c.WriteAsync(Header.Create(CODE, false).ToArray()).ConfigureAwait(false);
        }
    }
}
