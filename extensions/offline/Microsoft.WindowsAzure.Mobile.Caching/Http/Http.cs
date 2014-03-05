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
    }
}
