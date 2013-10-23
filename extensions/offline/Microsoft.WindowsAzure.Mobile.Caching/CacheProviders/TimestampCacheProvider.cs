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

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class TimestampCacheProvider : BaseCacheProvider
    {

        private readonly INetworkInformation network;
        private readonly IStructuredStorage storage;
        
        public TimestampCacheProvider(IStructuredStorage storage, INetworkInformation network)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");
            Contract.Requires<ArgumentNullException>(network != null, "network");

            this.network = network;
            this.storage = storage;
        }

        public override async Task<HttpContent> Read(Uri requestUri, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            //this will be our return object
            JObject json = new JObject();

            //make sure the timestamps are loaded
            await this.EnsureTimestampsLoaded();

            // get the tablename out of the uri
            string tableName = this.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                await this.Synchronize(requestUri, getResponse);

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
                HttpContent remoteResults = await getResponse(stampedRequestUri);
                string rawContent = await remoteResults.ReadAsStringAsync();
                json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidReadResponse(json);
                Debug.WriteLine("{0} returned:    {1}", stampedRequestUri, json);
                
                JToken newtimestamp;
                if (json.TryGetValue("timestamp", out newtimestamp))
                {
                    await this.SetLastTimestampForRequest(requestUri, newtimestamp.ToString());
                }
                //process changes and deletes
                JToken results;
                if (json.TryGetValue("results", out results))
                {
                    await this.storage.StoreData(tableName, results.Cast<IDictionary<string, JToken>>().Foreach(d => d["status"] = (int)ItemStatus.Unchanged));
                }
                JToken deleted;
                if (json.TryGetValue("deleted", out deleted))
                {
                    await this.storage.RemoveStoredData(tableName, deleted.Select(token => ((JValue)token).Value.ToString()));
                }
            }
            else
            {
                // add count in case of offline
                if (requestUri.OriginalString.Contains("$inlinecount=allpages"))
                {
                    json["count"] = -1;
                }
            }

            //parse uri into separate query options
            IQueryOptions queryOptions = new UriQueryOptions(requestUri);

            //send same query to local data store
            JArray data = null;
            try
            {
                IEnumerable<IDictionary<string, JToken>> localResults = await this.storage.GetStoredData(tableName, queryOptions);
                // filter locally deleted results
                data = JArray.Parse(JsonConvert.SerializeObject(localResults.Where(dict => (int)dict["status"] != (int)ItemStatus.Deleted)));
            }
            // catch error because we don't want to throw on missing columns when read
            catch (Exception)
            {
                data = new JArray();
            }

            //remove time stamp values
            json.Remove("timestamp");
            //remove deleted values
            json.Remove("deleted");

            //set the results
            json["results"] = data;

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
        public override async Task<HttpContent> Insert(Uri requestUri, HttpContent content, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string tableName = this.GetTableNameFromUri(requestUri);

            HttpContent response;
            JToken dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                await this.Synchronize(requestUri, getResponse);

                response = await base.Insert(requestUri, content, getResponse);
                string rawContent = await response.ReadAsStringAsync();
                JObject json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidInsertResponse(json);
                Debug.WriteLine("{0} returned:    {1}", requestUri, json);

                if (json.TryGetValue("results", out dataForInsertion))
                {
                    // result should be an array of a single item
                    // insert them as an unchanged items
                    dataForInsertion = dataForInsertion.Cast<JObject>().Foreach(d => d["status"] = (int)ItemStatus.Unchanged).FirstOrDefault();
                }
            }
            else
            {
                hasLocalChanges = true;

                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                dataForInsertion = JObject.Parse(rawContent);
                //return -1 as an id for local inserted items
                //normally this would be assigned by the server,
                //but, you know, we are offline
                dataForInsertion["id"] = -1;
                //set status to inserted
                dataForInsertion["status"] = (int)ItemStatus.Inserted;
                //wrap the object in an array
            }

            await this.storage.StoreData(tableName, new IDictionary<string, JToken>[] { (IDictionary<string, JToken>)dataForInsertion });

            Debug.WriteLine("Returning response:    {0}", dataForInsertion.ToString());

            return new StringContent(dataForInsertion.ToString());
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
        public override async Task<HttpContent> Update(Uri requestUri, HttpContent content, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");
            
            //Ensure guid
            string guidString = requestUri.GetQueryNameValuePairs().Where(kvp => kvp.Key.Equals("guid"))
                    .Select(kvp => kvp.Value)
                    .FirstOrDefault();
            if (string.IsNullOrEmpty(guidString))
            {
                throw new InvalidOperationException("In offline scenarios a guid is required for update operations.");
            }

            string tableName = this.GetTableNameFromUri(requestUri);
            
            HttpContent response;
            JToken dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                //make sure we synchronize
                await this.Synchronize(requestUri, getResponse);

                response = await base.Update(requestUri, content, getResponse);
                string rawContent = await response.ReadAsStringAsync();
                JObject json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidUpdateResponse(json);
                Debug.WriteLine("{0} returned:    {1}", requestUri, json);

                if(json.TryGetValue("results", out dataForInsertion))
                {
                    // result should be an array of objects, most of the time 1
                    // insert them as unchanged items.
                    dataForInsertion = dataForInsertion.Cast<JObject>().Foreach(d => d["status"] = (int)ItemStatus.Unchanged).FirstOrDefault();
                }
            }
            else
            {
                hasLocalChanges = true;
                
                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                dataForInsertion = JObject.Parse(rawContent);
                //if local item not known by server, it is still an insert
                if (requestUri.AbsolutePath.Contains("/-1"))
                {
                    dataForInsertion["status"] = (int)ItemStatus.Inserted;
                }
                else
                {
                    dataForInsertion["status"] = (int)ItemStatus.Changed;
                }
            }

            await this.storage.UpdateData(tableName, guidString, (IDictionary<string, JToken>)dataForInsertion);

            Debug.WriteLine("Returning response:    {0}", response);

            return response;
        }

        public override async Task<HttpContent> Delete(Uri requestUri, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string guidString = requestUri.GetQueryNameValuePairs().Where(kvp => kvp.Key.Equals("guid"))
                   .Select(kvp => kvp.Value)
                   .FirstOrDefault();
            if (string.IsNullOrEmpty(guidString))
            {
                throw new InvalidOperationException("In offline scenarios a guid is required for delete operations.");
            }

            string tableName = this.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                await this.Synchronize(requestUri, getResponse);

                HttpContent remoteResults = await base.Delete(requestUri, getResponse);
                string rawContent = await remoteResults.ReadAsStringAsync();
                JObject json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidDeleteResponse(json);
                Debug.WriteLine("{0} returned:    {1}", requestUri, json);

                JToken deleted;
                if (json.TryGetValue("deleted", out deleted))
                {
                    await this.storage.RemoveStoredData(tableName, deleted.Select(token => ((JValue)token).Value.ToString()));
                }
            }
            else
            {
                hasLocalChanges = true;
                               
                //if local item (-1) not known by server
                // we can just remove it locally and the server will never know about its existence
                if (requestUri.AbsolutePath.Contains("/-1"))
                {
                    await this.storage.RemoveStoredData(tableName, new[] { guidString });
                }
                else
                {
                    IEnumerable<IDictionary<string, JToken>> items = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery(string.Format("guid eq guid'{0}'", guidString)) });
                    await this.storage.StoreData(tableName, items.Foreach(d => d["status"] = (int)ItemStatus.Deleted));
                }
            }

            Debug.WriteLine("Returning response:    {0}", string.Empty);

            return new StringContent(string.Empty);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestUri"></param>
        /// <returns></returns>
        public override bool ProvidesCacheForRequest(Uri requestUri)
        {
            return requestUri.OriginalString.Contains("/tables/");
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
            IEnumerable<IDictionary<string, JToken>> knownUris = await this.storage.GetStoredData("timestamp_requests", new StaticQueryOptions());

            timestamps = new Dictionary<string, Tuple<string, string>>();
            foreach (var uri in knownUris)
            {
                this.timestamps.Add(uri["requesturl"].ToString(), Tuple.Create(uri["guid"].ToString(), uri["timestamp"].ToString()));
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

            Dictionary<string, JToken> item = new Dictionary<string, JToken>();
            item.Add("guid", timestampTuple.Item1);
            // required column so provide default
            item.Add("id", -1);
            item.Add("requesturl", requestUri.OriginalString);
            item.Add("timestamp", timestampTuple.Item2);
            //timestamps are local only so always unchanged
            item.Add("status", (int)ItemStatus.Unchanged);
            await this.storage.StoreData("timestamp_requests", new[] { item });

            //after successfull insert add locally
            this.timestamps[requestUri.OriginalString] = timestampTuple;
        }

        #endregion

        #region Sync

        /// <summary>
        /// We are going to be smart by using this value.
        /// </summary>
        private bool hasLocalChanges = true;

        private async Task Synchronize(Uri tableUri, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(tableUri != null, "tableUri");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            //return is there is nothing to sync
            if(!hasLocalChanges)
            {
                return;
            }

            string tableName = this.GetTableNameFromUri(tableUri);

            //sent all local changes 
            IEnumerable<IDictionary<string, JToken>> localChanges = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
            foreach (var item in localChanges)
            {
                JToken status;
                if (item.TryGetValue("status", out status))
                {
                    ItemStatus itemStatus = (ItemStatus)(int)status;
                    item.Remove("status");
                    //preform calls based on status: insert, change, delete 
                    switch (itemStatus)
                    {
                        case ItemStatus.Inserted:
                            //new items cannot have an id so remove any temporary id assigned by the client
                            item.Remove("id");
                            //remove any defined timestamp properties
                            item.Remove("timestamp");
                            await getResponse(tableUri, new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json"), HttpMethod.Post);
                            break;
                        case ItemStatus.Changed:
                            JToken id;
                            if (item.TryGetValue("id", out id))
                            {
                                await getResponse(new Uri(tableUri.OriginalString + "/" + id.ToString()), new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json"), new HttpMethod("PATCH"));
                            }
                            break;
                        case ItemStatus.Deleted:
                            JToken id2;
                            if (item.TryGetValue("id", out id2))
                            {
                                await getResponse(new Uri(tableUri.OriginalString + "/" + id2.ToString()), null, HttpMethod.Delete);
                            }
                            break;
                    };
                }

                //we have synchronized everything
                this.hasLocalChanges = false;
            }
        }

        #endregion
    }
}
