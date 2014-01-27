using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.MobileServices.Caching.CacheProviders;
using System.Diagnostics.Contracts;
using System.Collections;
using System.Diagnostics;
using System.Net;

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
        /// <param name="storage">The storage.</param>
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

        public override async Task<HttpContent> Read(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            //this will be our return object
            JObject json = new JObject();

            // get the tablename out of the uri
            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                //upload changes
                await this.synchronizer.UploadChanges(UriHelper.GetCleanTableUri(requestUri), this.Http);
                //download changes
                await this.synchronizer.DownloadChanges(requestUri, this.Http);
            }
            else
            {
                // add count in case of offline
                if (requestUri.OriginalString.Contains("$inlinecount=allpages"))
                {
                    json["count"] = -1;
                }
            }

            using (await this.storage.OpenAsync())
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
        public override async Task<HttpContent> Insert(Uri requestUri, HttpContent content)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");

            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            JToken result;
            string rawContent = await content.ReadAsStringAsync();
            JObject contentObject = JObject.Parse(rawContent);

            if (await network.IsConnectedToInternet())
            {
                Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                await this.synchronizer.UploadChanges(tableUri, this.Http);
                JObject json = await this.synchronizer.UploadInsert(contentObject, tableUri, this.Http);
                //insert expects a single item so we return the first one
                result = ResponseHelper.GetResultsJArrayFromJson(json).First;
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                contentObject["id"] = Guid.NewGuid().ToString();
                //set status to inserted
                contentObject["status"] = (int)ItemStatus.Inserted;

                using (await this.storage.OpenAsync())
                {
                    await this.storage.StoreData(tableName, new JArray(contentObject));
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
        public override async Task<HttpContent> Update(Uri requestUri, HttpContent content)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");

            string id = UriHelper.GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            JToken result;
            string rawContent = await content.ReadAsStringAsync();
            JObject contentObject = JObject.Parse(rawContent);

            if (await network.IsConnectedToInternet())
            {
                Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                //make sure we synchronize
                await this.synchronizer.UploadChanges(tableUri, this.Http);
                JObject json = await this.synchronizer.UploadUpdate(contentObject, tableUri, this.Http);
                //update expects a single item so we return the first one
                result = ResponseHelper.GetResultsJArrayFromJson(json).First;
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();
                //set status to changed
                contentObject["status"] = (int)ItemStatus.Changed;

                using (await this.storage.OpenAsync())
                {
                    await this.storage.UpdateData(tableName, new JArray(contentObject));
                }

                contentObject.Remove("status");

                result = contentObject;
            }

            Debug.WriteLine("Returning response:    {0}", result.ToString());

            return new StringContent(result.ToString(Formatting.None));
        }

        public override async Task<HttpContent> Delete(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string id = UriHelper.GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                Uri tableUri = UriHelper.GetCleanTableUri(requestUri);
                //make sure we synchronize
                await this.synchronizer.UploadChanges(tableUri, this.Http);
                JObject json = await this.synchronizer.UploadDelete(new JObject() { { "id", new JValue(id) } }, tableUri, this.Http);
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                //TODO: if local item is not known by server
                // we can just remove it locally and the server will never know about its existence
                using (await this.storage.OpenAsync())
                {
                    //update status of current item 
                    JArray arr = new JArray();
                    arr.Add(new JObject() { { "id", new JValue(id) }, { "status", new JValue((int)ItemStatus.Deleted) } });

                    await this.storage.UpdateData(tableName, arr);
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
    }
}
