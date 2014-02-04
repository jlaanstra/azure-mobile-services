using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    internal class Http : IHttp
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> func;

        public Http(Func<HttpRequestMessage, Task<HttpResponseMessage>> func)
        {
            this.func = func;
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return func(request);
        }

        public HttpRequestMessage OriginalRequest { get; set; }

        public HttpResponseMessage OriginalResponse { get; set; }

        public Task<HttpResponseMessage> SendOriginalAsync()
        {
            return SendAsync(this.OriginalRequest).ContinueWith(t =>
            {
                this.OriginalResponse = t.Result;
                return t.Result;
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        public HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IDictionary<string, string> headers = null)
        {
            HttpRequestMessage req = new HttpRequestMessage(method, uri);
            if (this.OriginalRequest != null)
            {
                foreach (var header in this.OriginalRequest.Headers)
                {
                    if (header.Key.StartsWith("X-ZUMO"))
                    {
                        req.Headers.Add(header.Key, header.Value);
                    }
                }

                req.Version = this.OriginalRequest.Version;
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
