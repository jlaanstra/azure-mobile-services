using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface ICacheProvider
    {
        bool ProvidesCacheForRequest(Uri requestUri);

        Task<HttpContent> Read(Uri requestUri, Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse);

        Task<HttpContent> Insert(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse);

        Task<HttpContent> Update(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse);

        Task<HttpContent> Delete(Uri requestUri, Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse);

        Task Synchronize(Uri tableUri, Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse);
    }
}
