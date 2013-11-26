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

        public override async Task<HttpContent> Read(Uri requestUri, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            //this will be our return object
            JObject json = new JObject();

            // get the tablename out of the uri
            string tableName = this.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                await this.synchronizer.Synchronize(tableName);

                //make sure the timestamps are loaded
                await this.EnsureTimestampsLoaded();

                //get latest known timestamp for a reqeust
                string timestamp = GetLastTimestampForRequest(requestUri);
                Uri stampedRequestUri;
                if (timestamp != null)
                {
                    stampedRequestUri = new Uri(string.Format("{0}&timestamp={1}", requestUri.OriginalString, Uri.EscapeDataString(timestamp)));
                }
                else
                {
                    stampedRequestUri = requestUri;
                }

                //send the timestamped request
                HttpContent remoteResults = await base.Read(stampedRequestUri, getResponse);
                json = await this.GetResponseAsJObject(remoteResults);
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
                JToken newtimestamp;
                if (json.TryGetValue("timestamp", out newtimestamp))
                {
                    await this.SetLastTimestampForRequest(requestUri, newtimestamp.ToString());
                }

                await this.storage.StoreData(tableName, this.GetResultsJArrayFromJson(json));
                await this.storage.RemoveStoredData(tableName, this.GetDeletedJArrayFromJson(json));

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
            json.Remove("timestamp");
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
        public override async Task<HttpContent> Insert(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string tableName = this.GetTableNameFromUri(requestUri);

            HttpContent response;
            JArray dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                await this.synchronizer.Synchronize(tableName);

                response = await base.Insert(requestUri, content, getResponse);
                JObject json = await this.GetResponseAsJObject(response);
                dataForInsertion = this.GetResultsJArrayFromJson(json);
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                JObject result = JObject.Parse(rawContent);
                result["id"] = Guid.NewGuid().ToString();
                //set status to inserted
                result["status"] = (int)ItemStatus.Inserted;
                //wrap the object in an array
                dataForInsertion = new JArray(result);
            }

            using (await this.storage.OpenAsync())
            {
                await this.storage.StoreData(tableName, dataForInsertion);
            }

            //insert expects a single item so we return the first one
            JToken returnResult = dataForInsertion.First;

            Debug.WriteLine("Returning response:    {0}", returnResult.ToString());

            return new StringContent(returnResult.ToString());
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
        public override async Task<HttpContent> Update(Uri requestUri, HttpContent content, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string id = GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            string tableName = this.GetTableNameFromUri(requestUri);

            HttpContent response;
            JArray dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                //make sure we synchronize
                await this.synchronizer.Synchronize(tableName);

                response = await base.Update(requestUri, content, getResponse);
                JObject json = await this.GetResponseAsJObject(response);
                dataForInsertion = this.GetResultsJArrayFromJson(json);
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                JObject result = JObject.Parse(rawContent);
                result["status"] = (int)ItemStatus.Changed;
                dataForInsertion = new JArray(result);
            }

            using (await this.storage.OpenAsync())
            {
                await this.storage.UpdateData(tableName, dataForInsertion);
            }

            //insert expects a single item so we return the first one
            JToken returnResult = dataForInsertion.First;

            Debug.WriteLine("Returning response:    {0}", returnResult.ToString());

            return new StringContent(returnResult.ToString());
        }

        public override async Task<HttpContent> Delete(Uri requestUri, Func<Uri, HttpContent, HttpMethod, IDictionary<string, string>, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string id = GetIdFromUri(requestUri);
            if (string.IsNullOrEmpty(id))
            {
                throw new InvalidOperationException("Can't retrieve id from Uri.");
            }

            string tableName = this.GetTableNameFromUri(requestUri);

            IEnumerable<string> idsToRemove;

            if (await network.IsConnectedToInternet())
            {
                await this.synchronizer.Synchronize(tableName);

                HttpContent remoteResults = await base.Delete(requestUri, getResponse);
                JObject json = await this.GetResponseAsJObject(remoteResults);
                idsToRemove = this.GetDeletedJArrayFromJson(json);
            }
            else
            {
                this.synchronizer.NotifyOfUnsynchronizedChange();

                //if local item (-1) not known by server
                // we can just remove it locally and the server will never know about its existence
                idsToRemove = new string[0];
            }

            using (await this.storage.OpenAsync())
            {
                await this.storage.RemoveStoredData(tableName, idsToRemove);

                //update status of current item 
                JArray arr = new JArray();
                arr.Add(new JObject() { { "id", new JValue(id) }, { "status", new JValue((int)ItemStatus.Deleted) } });

                await this.storage.UpdateData(tableName, arr);
            }

            Debug.WriteLine("Returning response:    {0}", string.Empty);

            return new StringContent(string.Empty);
        }

        private async Task<JObject> GetResponseAsJObject(HttpContent response)
        {
            string rawContent = await response.ReadAsStringAsync();
            JObject json = JObject.Parse(rawContent);
            ResponseHelper.EnsureValidSyncResponse(json);
            Debug.WriteLine("Reponse returned:    {0}", json);
            return json;
        }

        private JArray GetResultsJArrayFromJson(JObject json)
        {
            JArray dataForInsertion;

            JToken results;
            if (json.TryGetValue("results", out results) && results is JArray)
            {
                // result should be an array of a single item
                // insert them as an unchanged items
                dataForInsertion = (JArray)results;
                foreach (JObject item in dataForInsertion)
                {
                    item["status"] = (int)ItemStatus.Unchanged;
                }
            }
            else
            {
                dataForInsertion = new JArray();
            }

            return dataForInsertion;
        }

        private IEnumerable<string> GetDeletedJArrayFromJson(JObject json)
        {
            JToken deleted;
            if (json.TryGetValue("deleted", out deleted))
            {
                return deleted.Select(token => ((JValue)token).Value.ToString());
            }
            else
            {
                return new string[0];
            }
        }

        private string GetIdFromUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string path = requestUri.AbsolutePath;
            int lastSlash = path.LastIndexOf('/');
            return path.Substring(lastSlash + 1);
        }

        private string GetTableNameFromUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string tables = "/tables/";
            string path = requestUri.AbsolutePath;
            int startIndex = path.IndexOf(tables) + tables.Length;
            int endIndex = path.IndexOf('/', startIndex);
            if (endIndex == -1)
            {
                endIndex = path.Length;
            }
            return path.Substring(startIndex, endIndex - startIndex);
        }

        private Uri GetCleanTableUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string tables = "/tables/";
            string url = requestUri.OriginalString;
            //remove query
            url = url.Split('?').First();
            int endIndex = url.IndexOf('/', url.IndexOf(tables) + tables.Length);
            if (endIndex == -1)
            {
                endIndex = url.Length;
            }
            return new Uri(url.Substring(0, endIndex));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public override bool ProvidesCacheForRequest(Uri requestUri)
        {
            return !requestUri.OriginalString.Contains("sync=1") && requestUri.OriginalString.Contains("/tables/") && areWeCachingThis(requestUri);
        }

        #region Timestamps

        private Dictionary<string, Tuple<string, string>> timestamps;

        private Task EnsureTimestampsLoaded()
        {
            if (timestamps == null)
            {
                return this.LoadTimestamps();
            }
            return Task.FromResult(0);
        }

        private async Task LoadTimestamps()
        {
            using (await this.storage.OpenAsync())
            {
                JArray knownUris = await this.storage.GetStoredData("timestamp_requests", new StaticQueryOptions());

                timestamps = new Dictionary<string, Tuple<string, string>>();
                foreach (var uri in knownUris)
                {
                    this.timestamps.Add(uri["requesturl"].ToString(), Tuple.Create(uri["guid"].ToString(), uri["timestamp"].ToString()));
                }
            }
        }

        private string GetLastTimestampForRequest(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            Tuple<string, string> timestampTuple;
            if (this.timestamps.TryGetValue(requestUri.OriginalString, out timestampTuple))
            {
                return timestampTuple.Item2;
            }
            else
            {
                return null;
            }
        }

        private async Task SetLastTimestampForRequest(Uri requestUri, string timestamp)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<InvalidOperationException>(this.timestamps != null, "Timestamps where not loaded. Make sure you called LoadTimestamps() before calling this method.");

            Tuple<string, string> timestampTuple;
            if (!this.timestamps.TryGetValue(requestUri.OriginalString, out timestampTuple))
            {
                //if not exists create new one
                timestampTuple = Tuple.Create(Guid.NewGuid().ToString(), timestamp);
            }
            else
            {
                //if exists reuse guid key
                timestampTuple = Tuple.Create(timestampTuple.Item1, timestamp);
            }

            JObject item = new JObject();
            item.Add("id", timestampTuple.Item1);
            // required column so provide default
            item.Add("requesturl", requestUri.OriginalString);
            item.Add("__version", timestampTuple.Item2);
            //timestamps are local only so always unchanged
            item.Add("status", (int)ItemStatus.Unchanged);
            await this.storage.StoreData("timestamp_requests", new JArray(item));

            //after successfull insert add locally
            this.timestamps[requestUri.OriginalString] = timestampTuple;
        }

        #endregion
    }
}
