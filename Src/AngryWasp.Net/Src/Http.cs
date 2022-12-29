using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AngryWasp.Net
{
    public static class Http
    {
        public class HttpRequestReturnData<T>
        {
            public bool HasError { get; set; } = false;

            public int StatusCode { get; set; } = 0;

            public T Data { get; set; }
        }

        public static async Task<HttpRequestReturnData<string>> HttpRequestAsync(string url, int redirectLevels = 0)
        {
            try
            {
                var response = await GetResponse(url, redirectLevels).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new HttpRequestReturnData<string> { HasError = false, Data = content, StatusCode = (int)response.StatusCode };
                }
                else
                    return new HttpRequestReturnData<string> { HasError = true, Data = null, StatusCode = (int)response.StatusCode };
            }
            catch (Exception ex)
            {
                return new HttpRequestReturnData<string> { HasError = true, Data = ex.Message, StatusCode = -1 };
            }
        }

        public static async Task<HttpRequestReturnData<byte[]>> HttpRequestAsByteArrayAsync(string url, int redirectLevels = 0)
        {
            try
            {
                var response = await GetResponse(url, redirectLevels).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    return new HttpRequestReturnData<byte[]> { HasError = false, Data = content, StatusCode = (int)response.StatusCode };
                }
                else
                    return new HttpRequestReturnData<byte[]> { HasError = true, Data = null, StatusCode = (int)response.StatusCode };
            }
            catch (Exception ex)
            {
                return new HttpRequestReturnData<byte[]> { HasError = true, Data = Encoding.ASCII.GetBytes(ex.Message), StatusCode = -1 };
            }
        }

        private static async Task<HttpResponseMessage> GetResponse(string url, int redirectLevels = 0)
        {
            HttpClient client;

            if (redirectLevels == 0)
                client = new HttpClient();
            else
                client = new HttpClient(new HttpClientHandler
                {
                    AllowAutoRedirect = true,
                    MaxAutomaticRedirections = redirectLevels
                });

            return await client.GetAsync(url).ConfigureAwait(false);
        }
    }
}