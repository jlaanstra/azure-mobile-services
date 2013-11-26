using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public abstract class BaseCacheProvider : ICacheProvider
    {
        public virtual Task<HttpContent> Read(Uri requestUri, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            return getResponse(requestUri, null, HttpMethod.Get, null);
        }

        public virtual Task<HttpContent> Insert(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            return getResponse(requestUri, content, HttpMethod.Post, null);
        }

        public virtual Task<HttpContent> Update(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            return getResponse(requestUri, content, new HttpMethod("PATCH"), null);
        }

        public virtual Task<HttpContent> Delete(Uri requestUri, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            return getResponse(requestUri, null, HttpMethod.Delete, null);
        }

        public virtual bool ProvidesCacheForRequest(Uri requestUri)
        {
            return false;
        }
    }
}
