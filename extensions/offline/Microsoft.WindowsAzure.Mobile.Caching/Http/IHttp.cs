using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface IHttp
    {
        HttpRequestMessage OriginalRequest { get; set; }

        HttpResponseMessage OriginalResponse { get; set; }

        Task<HttpResponseMessage> SendOriginalAsync();

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);

        HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IDictionary<string, string> headers = null);
    }

    public static class IHttpExtensions
    {
        public static async Task<JObject> GetJsonAsync(this IHttp This, HttpRequestMessage request)
        {
            HttpResponseMessage response = await This.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                throw new HttpStatusCodeException(response.StatusCode, error);
            }

            HttpContent responseContent = response.Content;

            // cleanup the request
            request.Dispose();

            return await ResponseHelper.GetResponseAsJObject(responseContent);
        }
    }
}
