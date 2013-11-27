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

        Task<HttpContent> Read(Uri requestUri);

        Task<HttpContent> Insert(Uri requestUri, HttpContent content);

        Task<HttpContent> Update(Uri requestUri, HttpContent content);

        Task<HttpContent> Delete(Uri requestUri);

        IHttp Http { get; set; }
    }
}
