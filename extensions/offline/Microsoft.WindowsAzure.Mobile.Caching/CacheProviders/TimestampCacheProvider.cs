using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.Contracts;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class TimestampCacheProvider : BaseCacheProvider
    {
        private readonly INetworkInformation network;
        private readonly IStructuredStorage storage;
        private readonly ISynchronizer synchronizer;
        private readonly Func<Uri, bool> areWeCachingThis;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimestampCacheProvider"/> class.
        /// </summary>
        /// <param name="storage">The storage for this cacheprovider.</param>
        /// <param name="network">The network.</param>
        /// <param name="synchronizer">The synchronizer.</param>
        /// <param name="areWeCachingThis">The are we caching this.</param>
        public TimestampCacheProvider(IStructuredStorage storage, INetworkInformation network, ISynchronizer synchronizer, Func<Uri, bool> areWeCachingThis = null)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");
            Contract.Requires<ArgumentNullException>(network != null, "network");
            Contract.Requires<ArgumentNullException>(synchronizer != null, "synchronizer");

            this.network = network;
            this.storage = storage;
            this.synchronizer = synchronizer;
            this.areWeCachingThis = areWeCachingThis ?? (u => true);
        }

        public override async Task<HttpContent> Read(Uri requestUri, IHttp http)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            //this will be our return object
            JObject json = new JObject();

            // get the tablename out of the uri
            string tableName = UriHelper.GetTableNameFromUri(requestUri);
            //LookupAsync adds id
            string id = UriHelper.GetIdFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                //upload changes
                await this.synchronizer.UploadChanges(UriHelper.GetCleanTableUri(requestUri), http);
                //download changes
                JToken count;
                JObject obj = await this.synchronizer.DownloadChanges(requestUri, http);
                if(obj.TryGetValue("count", out count))
                {
                    json["count"] = count;
                }
                else
                {
                    json["count"] = -1;
                }
            }
            else
            {
                json["count"] = -1;
            }

            using (await this.storage.Open())
            {
                //parse uri into separate query options
                IQueryOptions uriQueryOptions = new UriQueryOptions(requestUri);
                IQueryOptions queryOptions = new StaticQueryOptions()
                {
                    Filter = uriQueryOptions.Filter != null ?
                        new FilterQuery(string.Format("{0} and status ne {1}", uriQueryOptions.Filter.RawValue, (int)ItemStatus.Deleted))
                        : new FilterQuery(string.Format("status ne {0}", (int)ItemStatus.Deleted)),
                    InlineCount = uriQueryOptions.InlineCount,
                    OrderBy = uriQueryOptions.OrderBy,
                    Skip = uriQueryOptions.Skip,
                    Top = uriQueryOptions.Top,
                };

                //send same query to local data store with the addition of
                JArray data = null;
                try
                {
                    data = await this.storage.GetStoredData(tableName, queryOptions);
                }
                // catch error because we don't want to throw on missing columns when read
                catch (Exception)
                {
                    data = new JArray();
                }

                //set the results
                json["results"] = data;
            }

            //remove time stamp values
            json.Remove("__version");
            //remove deleted values
            json.Remove("deleted");

            Debug.WriteLine("Returning response:    {0}", json.ToString());

            //this json should be in the format that the MobileServiceClient expects
            return new StringContent(json.ToString());
        }

        /// <summary>
        /// Handles cached inserts. Only one item can be inserted at a time.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content.</param>
        /// <param name="getResponse">The get response.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Invalid response.</exception>
        public override async Task<HttpContent> Insert(Uri requestUri, HttpContent content, IHttp http)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");

            string tableName = UriHelper.GetTableNameFromUri(requestUri);
            IDictionary<string, string> parameters = UriHelper.GetQueryParameters(requestUri);

            JToken result;
            string rawContent = await content.ReadAsStringAsync();
            JObject contentObject = JObject.Parse(rawContent);

            //ensure the item has an id
            string id = contentObject.Value<string>("id");
            if (string.IsNullOrEmpty(id))
            {
                id = Guid.NewGuid().ToString();
                contentObject["id"] = id;
            }

            if (await network.IsConnectedToInternet())
            {
                Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                contentObject["status"] = (int)ItemStatus.Inserted;
                JObject json = await this.synchronizer.UploadChanges(tableUri, http, contentObject, parameters);
                //insert expects a single item so we return the first one
                result = ResponseHelper.GetResultsJArrayFromJson(json).FirstOrDefault(t => t.Value<string>("id").Equals(id));
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();
                
                contentObject["__version"] = null;
                //set status to inserted
                contentObject["status"] = (int)ItemStatus.Inserted;

                using (await this.storage.Open())
                {
                    await this.storage.StoreData(tableName, new JArray(contentObject), false);
                }

                contentObject.Remove("status");

                result = contentObject;
            }
            
            Debug.WriteLine("Returning response:    {0}", result.ToString());

            return new StringContent(result.ToString(Formatting.None));
        }

        /// <summary>
        /// Handles cached changes.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content.</param>
        /// <param name="getResponse">The get response.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">
        /// In offline scenarios a guid is required for update operations.
        /// or
        /// Invalid response.
        /// </exception>
        public override async Task<HttpContent> Update(Uri requestUri, HttpContent content, IHttp http)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");

            // get id
            string id = UriHelper.GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            JToken result;
            string rawContent = await content.ReadAsStringAsync();
            JObject contentObject = JObject.Parse(rawContent);

            // set __version
            if (http.OriginalRequest != null)
            {
                EntityTagHeaderValue tag = http.OriginalRequest.Headers.IfMatch.FirstOrDefault();
                if (tag != null)
                {
                    //trim "
                    contentObject["__version"] = tag.Tag.Trim(new char[] { '"' });
                }
            }

            // get parameters
            IDictionary<string, string> parameters = UriHelper.GetQueryParameters(requestUri);
            
            if (await network.IsConnectedToInternet())
            {
                Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                //make sure we synchronize
                contentObject["status"] = (int)ItemStatus.Changed;
                JObject json = await this.synchronizer.UploadChanges(tableUri, http, contentObject, parameters);
                //update expects a single item so we return the first one that matches the id
                result = ResponseHelper.GetResultsJArrayFromJson(json).FirstOrDefault(t => t.Value<string>("id").Equals(id));
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();
                
                string tableName = UriHelper.GetTableNameFromUri(requestUri);

                using (await this.storage.Open())
                {
                    JObject arr = await this.storage.GetItemWithId(tableName, id);
                    if (arr != null && arr.Value<int>("status") == (int)ItemStatus.Unchanged)
                    {
                        //set status to changed
                        contentObject["status"] = (int)ItemStatus.Changed;
                    }
                    await this.storage.UpdateData(tableName, new JArray(contentObject));
                }

                contentObject.Remove("status");

                result = contentObject;
            }

            Debug.WriteLine("Returning response:    {0}", result.ToString());

            return new StringContent(result.ToString(Formatting.None));
        }

        public override async Task<HttpContent> Delete(Uri requestUri, IHttp http)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            // get id
            string id = UriHelper.GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            // get parameters
            IDictionary<string, string> parameters = UriHelper.GetQueryParameters(requestUri);
            JObject item = null;
            using (await this.storage.Open())
            {
                item = await this.storage.GetItemWithId(tableName, id);
            }

            if (await network.IsConnectedToInternet())
            {
                if (item != null)
                {
                    Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                    item["status"] = (int)ItemStatus.Deleted;
                    //make sure we synchronize
                    JObject res = await this.synchronizer.UploadChanges(tableUri, http, item, parameters);
                }
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                //TODO: if local item is not known by server
                // we can just remove it locally and the server will never know about its existence
                using (await this.storage.Open())
                {
                    if (item != null && item.Value<int>("status") != (int)ItemStatus.Inserted)
                    {
                        //update status of current item 
                        JArray deleted = new JArray();
                        deleted.Add(new JObject() { { "id", new JValue(id) }, { "status", new JValue((int)ItemStatus.Deleted) } });

                        await this.storage.UpdateData(tableName, deleted);
                    }
                    else
                    {
                        await this.storage.RemoveStoredData(tableName, new JArray(id));
                    }                    
                }
            }            

            Debug.WriteLine("Returning response:    {0}", string.Empty);

            return new StringContent(string.Empty);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public override bool ProvidesCacheForRequest(Uri requestUri)
        {
            return requestUri.OriginalString.Contains("/tables/") && areWeCachingThis(requestUri);
        }

        public override async Task Purge()
        {
            using (await this.storage.Open())
            {
                await this.storage.Purge();
            }
        }
    }
}
