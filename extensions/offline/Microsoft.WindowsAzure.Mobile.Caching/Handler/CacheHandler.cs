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
        private readonly ICacheProvider cache;
        private readonly IHttp http;

        private static readonly AsyncLock @lock = new AsyncLock();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cache"></param>
        public CacheHandler(ICacheProvider cache)
        {
            this.cache = cache;
            this.http = new Http(m => base.SendAsync(m, CancellationToken.None));
            this.cache.Http = http;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            //are we caching request for this URI?
            if (!cache.ProvidesCacheForRequest(request.RequestUri))
            {
                return await base.SendAsync(request, cancellationToken);
            }
            else
            {
                HttpContent newContent = null;
                HttpResponseMessage response = null;

                using (await @lock.LockAsync())
                {
                    this.http.OriginalRequest = request;

                    switch (request.Method.Method)
                    {
                        case "GET":
                            newContent = await cache.Read(request.RequestUri);
                            break;

                        case "POST":
                            newContent = await cache.Insert(request.RequestUri, request.Content);
                            break;

                        case "PATCH":
                            newContent = await cache.Update(request.RequestUri, request.Content);
                            break;

                        case "DELETE":
                            newContent = await cache.Delete(request.RequestUri);
                            break;
                        default:
                            newContent = request.Content;
                            break;
                    }

                    response = this.http.OriginalResponse;

                    //explicitly null these out for the possible next request
                    this.http.OriginalRequest = null;
                    this.http.OriginalResponse = null;
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (response == null)
                {
                    response = new HttpResponseMessage(HttpStatusCode.OK);
                }
                response.Content = newContent;

                return response;
            }
        }

        ////closure capturing the actual base SendAsync
        //Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> sendAsync = async (uri, content, method, headers) =>
        //{
        //    //dispose old responses
        //    if (response != null)
        //    {
        //        response.Dispose();
        //    }

        //    uri = uri ?? request.RequestUri;
        //    method = method ?? request.Method;
        //    HttpRequestMessage req = new HttpRequestMessage(method, uri);
        //    req.Content = content;
        //    foreach (var header in request.Headers)
        //    {
        //        req.Headers.Add(header.Key, header.Value);
        //    }
        //    if (headers != null)
        //    {
        //        foreach (var header in headers)
        //        {
        //            req.Headers.Add(header.Key, header.Value);
        //        }
        //    }
        //    req.Version = request.Version;
        //    foreach (var property in request.Properties)
        //    {
        //        req.Properties.Add(property.Key, property.Value);
        //    }

        //    //we use our own cancellation, because sync should never be cancelled
        //    response = await base.SendAsync(req, cts.Token);
        //    // Throw errors for any failing responses
        //    if (!response.IsSuccessStatusCode)
        //    {
        //        string error = await response.Content.ReadAsStringAsync();
        //        throw new HttpStatusCodeException(response.StatusCode, error);
        //    }

        //    HttpContent responseContent = response.Content;

        //    // cleanup the request
        //    req.Dispose();

        //    return responseContent;
        //};
    }
}
