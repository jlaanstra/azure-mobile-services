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

        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request);
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

            JObject result = await ResponseHelper.GetResponseAsJObject(responseContent);

            // cleanup the request
            request.Dispose();
            response.Dispose();

            return result;
        }

        public static Task<HttpResponseMessage> SendOriginalAsync(this IHttp This)
        {
            return This.SendAsync(This.OriginalRequest).ContinueWith(t =>
            {
                This.OriginalResponse = t.Result;
                return t.Result;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        
        public static HttpRequestMessage CreateRequest(this IHttp This, HttpMethod method, Uri uri, IDictionary<string, string> headers = null)
        {
            HttpRequestMessage req = new HttpRequestMessage(method, uri);
            if (This.OriginalRequest != null)
            {
                foreach (var header in This.OriginalRequest.Headers)
                {
                    if (header.Key.StartsWith("X-ZUMO"))
                    {
                        req.Headers.Add(header.Key, header.Value);
                    }
                }

                req.Version = This.OriginalRequest.Version;
            }
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    req.Headers.Add(header.Key, header.Value);
                }
            }
            return req;
        }
    }
}
