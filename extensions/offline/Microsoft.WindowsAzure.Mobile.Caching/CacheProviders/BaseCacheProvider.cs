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
        public virtual async Task<HttpContent> Read(Uri requestUri, IHttp http)
        {
            http.OriginalRequest.RequestUri = requestUri;
            HttpResponseMessage response = await http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Insert(Uri requestUri, HttpContent content, IHttp http)
        {
            http.OriginalRequest.RequestUri = requestUri;
            http.OriginalRequest.Content = content;
            HttpResponseMessage response = await http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Update(Uri requestUri, HttpContent content, IHttp http)
        {
            http.OriginalRequest.RequestUri = requestUri;
            http.OriginalRequest.Content = content;
            HttpResponseMessage response = await http.SendOriginalAsync();
            return response.Content;
        }

        public virtual async Task<HttpContent> Delete(Uri requestUri, IHttp http)
        {
            http.OriginalRequest.RequestUri = requestUri;
            HttpResponseMessage response = await http.SendOriginalAsync();
            return response.Content;
        }

        public virtual bool ProvidesCacheForRequest(Uri requestUri)
        {
            return false;
        }
    }
}
