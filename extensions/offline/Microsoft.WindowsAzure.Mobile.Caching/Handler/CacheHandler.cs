using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class CacheHandler : DelegatingHandler
    {
        private readonly ICacheProvider[] caches;

        private static readonly AsyncLock @lock = new AsyncLock();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cache"></param>
        public CacheHandler(params ICacheProvider[] caches)
        {
            this.caches = caches;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // coose the first cacheprovider to handle the request
            ICacheProvider provider = caches.FirstOrDefault(c => c.ProvidesCacheForRequest(request.RequestUri));

            //are we caching request for this URI?
            if (provider == null)
            {
                return await base.SendAsync(request, cancellationToken);
            }
            else
            {
                HttpContent newContent = null;
                HttpResponseMessage response = null;

                // this lock should eventually go away
                using (await @lock.LockAsync())
                {
                    Http http = new Http(m => base.SendAsync(m, CancellationToken.None));
                    http.OriginalRequest = request;

                    switch (request.Method.Method)
                    {
                        case "GET":
                            newContent = await provider.Read(request.RequestUri, http);
                            break;

                        case "POST":
                            newContent = await provider.Insert(request.RequestUri, request.Content, http);
                            break;

                        case "PATCH":
                            newContent = await provider.Update(request.RequestUri, request.Content, http);
                            break;

                        case "DELETE":
                            newContent = await provider.Delete(request.RequestUri, http);
                            break;
                        default:
                            throw new NotImplementedException();
                            break;
                    }

                    response = http.OriginalResponse;
                }

                if (response == null)
                {
                    response = new HttpResponseMessage(HttpStatusCode.OK);
                }
                response.Content = newContent;

                return response;
            }
        }
    }
}
