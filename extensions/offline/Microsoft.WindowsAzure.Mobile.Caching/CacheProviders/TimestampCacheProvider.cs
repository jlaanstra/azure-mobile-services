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

            // get the tablename out of the uri
            string tableName = this.GetTableNameFromUri(requestUri);

            if (await network.IsConnectedToInternet())
            {
                await this.Synchronize(requestUri, getResponse);
                
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
                HttpContent remoteResults = await getResponse(stampedRequestUri);
                string rawContent = await remoteResults.ReadAsStringAsync();
                json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidReadResponse(json);
                Debug.WriteLine("{0} returned:    {1}", stampedRequestUri, json);
            }
            else
            {
                // add count in case of offline
                if (requestUri.OriginalString.Contains("$inlinecount=allpages"))
                {
                    json["count"] = -1;
                }
            }

            using(await this.storage.OpenAsync())
            {
                JToken newtimestamp;
                if (json.TryGetValue("timestamp", out newtimestamp))
                {
                    await this.SetLastTimestampForRequest(requestUri, newtimestamp.ToString());
                }
                //process changes and deletes
                JToken results;
                if (json.TryGetValue("results", out results) && results is JArray)
                {
                    JArray dataForInsertion = (JArray)results;
                    foreach(JObject item in dataForInsertion)
                    {
                        item["status"] = (int)ItemStatus.Unchanged;
                    }
                    await this.storage.StoreData(tableName, dataForInsertion);
                }
                JToken deleted;
                if (json.TryGetValue("deleted", out deleted))
                {
                    await this.storage.RemoveStoredData(tableName, deleted.Select(token => ((JValue)token).Value.ToString()));
                }

                //parse uri into separate query options
                IQueryOptions uriQueryOptions = new UriQueryOptions(requestUri);
                IQueryOptions queryOptions = new StaticQueryOptions()
                {
                    Filter = new FilterQuery(string.Format("{0} and status ne {1}", uriQueryOptions.Filter.RawValue, (int)ItemStatus.Deleted)),
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
        public override async Task<HttpContent> Insert(Uri requestUri, HttpContent content, OptionalFunc<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            Contract.Requires<ArgumentNullException>(content != null, "content");
            Contract.Requires<ArgumentNullException>(getResponse != null, "getResponse");

            string tableName = this.GetTableNameFromUri(requestUri);

            HttpContent response;
            JArray dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                await this.Synchronize(requestUri, getResponse);

                response = await base.Insert(requestUri, content, getResponse);
                string rawContent = await response.ReadAsStringAsync();
                JObject json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidInsertResponse(json);
                Debug.WriteLine("{0} returned:    {1}", requestUri, json);
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
            }
            else
            {
                hasLocalChanges = true;

                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                JObject result = JObject.Parse(rawContent);
                //return -1 as an id for local inserted items
                //normally this would be assigned by the server,
                //but, you know, we are offline
                result["id"] = -1;
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
            JArray dataForInsertion;

            if (await network.IsConnectedToInternet())
            {
                //make sure we synchronize
                await this.Synchronize(requestUri, getResponse);

                response = await base.Update(requestUri, content, getResponse);
                string rawContent = await response.ReadAsStringAsync();
                JObject json = JObject.Parse(rawContent);
                ResponseValidator.EnsureValidUpdateResponse(json);
                Debug.WriteLine("{0} returned:    {1}", requestUri, json);
                JToken results;
                if(json.TryGetValue("results", out results) && results is JArray)
                {
                    // result should be an array of objects, mostly a single one
                    // insert them as unchanged items.
                    dataForInsertion = (JArray)results;
                    foreach(JObject item in dataForInsertion)
                    {
                        item["status"] = (int)ItemStatus.Unchanged;
                    }
                }
                else        
                {
                    dataForInsertion = new JArray();
                }
            }
            else
            {
                hasLocalChanges = true;
                
                string rawContent = await content.ReadAsStringAsync();
                response = new StringContent(rawContent);
                JObject result = JObject.Parse(rawContent);
                //if local item not known by server, it is still an insert
                if (requestUri.AbsolutePath.Contains("/-1"))
                {
                    result["status"] = (int)ItemStatus.Inserted;
                }
                else
                {
                    result["status"] = (int)ItemStatus.Changed;
                }
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

            IEnumerable<string> guidsToRemove;

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
                    guidsToRemove = deleted.Select(token => ((JValue)token).Value.ToString());
                }
                else
                {
                    guidsToRemove = new string[0];
                }
            }
            else
            {
                hasLocalChanges = true;
                               
                //if local item (-1) not known by server
                // we can just remove it locally and the server will never know about its existence
                if (requestUri.AbsolutePath.Contains("/-1"))
                {
                   guidsToRemove = new[] { guidString };
                }
                else
                {
                    guidsToRemove = new string[0];
                }
            }

            using (await this.storage.OpenAsync())
            {
                await this.storage.RemoveStoredData(tableName, guidsToRemove);

                JArray arr = new JArray();
                foreach(var guid in guidsToRemove)
                {
                    arr.Add(new JObject() { { "guid", new JValue(guid) }, { "status", new JValue((int)ItemStatus.Deleted) } });
                }

                await this.storage.UpdateData(tableName, arr);
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
            item.Add("guid", timestampTuple.Item1);
            // required column so provide default
            item.Add("id", -1);
            item.Add("requesturl", requestUri.OriginalString);
            item.Add("timestamp", timestampTuple.Item2);
            //timestamps are local only so always unchanged
            item.Add("status", (int)ItemStatus.Unchanged);
            await this.storage.StoreData("timestamp_requests", new JArray(item));

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

            // all communication with the database should be on the same thread
            using (await this.storage.OpenAsync())
            {
                //sent all local changes 
                JArray localChanges = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
                foreach (JObject item in localChanges)
                {
                    JToken status;
                    HttpStatusCode code = HttpStatusCode.OK;
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

                                try
                                {
                                    await getResponse(tableUri, new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json"), HttpMethod.Post);
                                }
                                catch(HttpStatusCodeException ex)
                                {
                                    code = ex.StatusCode;
                                }
                                //item already exists on the server
                                if(code == HttpStatusCode.Conflict)
                                {
                                    await this.storage.RemoveStoredData(tableName, new[] { item["guid"].ToString().ToLowerInvariant() });
                                }
                                break;
                            case ItemStatus.Changed:
                                JToken id;
                                if (item.TryGetValue("id", out id))
                                {
                                    try
                                    {
                                        await getResponse(new Uri(tableUri.OriginalString + "/" + id.ToString()), new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json"), new HttpMethod("PATCH"));
                                    }
                                    catch (HttpStatusCodeException ex)
                                    {
                                        code = ex.StatusCode;
                                    }
                                }
                                break;
                            case ItemStatus.Deleted:
                                JToken id2;
                                if (item.TryGetValue("id", out id2))
                                {
                                    try
                                    {
                                        await getResponse(new Uri(tableUri.OriginalString + "/" + id2.ToString()), null, HttpMethod.Delete);
                                    }
                                    catch (HttpStatusCodeException ex)
                                    {
                                        code = ex.StatusCode;
                                    }
                                }
                                break;
                        };
                    }

                    //we have synchronized everything
                    this.hasLocalChanges = false;
                }
            }
      }

        #endregion
    }
}
