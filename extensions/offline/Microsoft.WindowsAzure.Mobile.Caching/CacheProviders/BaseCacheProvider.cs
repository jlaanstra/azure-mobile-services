using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public abstract class BaseCacheProvider : ICacheProvider
    {
        public virtual async Task<HttpContent> Read(Uri requestUri)
        {
            this.Http.OriginalRequest.RequestUri = requestUri;
            HttpResponseMessage response = await this.Http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Insert(Uri requestUri, HttpContent content)
        {
            this.Http.OriginalRequest.RequestUri = requestUri;
            this.Http.OriginalRequest.Content = content;
            HttpResponseMessage response = await this.Http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Update(Uri requestUri, HttpContent content)
        {
            this.Http.OriginalRequest.RequestUri = requestUri;
            this.Http.OriginalRequest.Content = content;
            HttpResponseMessage response = await this.Http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Delete(Uri requestUri)
        {
            this.Http.OriginalRequest.RequestUri = requestUri;
            HttpResponseMessage response = await this.Http.SendOriginalAsync();
            return response.Content;
        }

        public virtual bool ProvidesCacheForRequest(Uri requestUri)
        {
            return false;
        }

        public IHttp Http { get; set; }
    }
}
