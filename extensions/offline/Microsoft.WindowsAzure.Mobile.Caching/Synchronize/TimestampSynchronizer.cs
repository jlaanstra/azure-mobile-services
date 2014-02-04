using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class TimestampSynchronizer : ISynchronizer
    {
        private readonly IStructuredStorage storage;
        private readonly AsyncLazy<bool> timestampsLoaded;

        /// <summary>
        /// We are going to be smart by using this value.
        /// </summary>
        /// <param name="storage">The storage.</param>
        private bool hasLocalChanges = true;
        
        public event EventHandler<Conflict> Conflict;

        protected virtual void OnConflict(Conflict conflict)
        {
            var handler = this.Conflict;
            if(handler != null)
            {
                handler(this, conflict);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TimestampSynchronizer"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        public TimestampSynchronizer(IStructuredStorage storage)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");

            this.storage = storage;

            this.timestampsLoaded = new AsyncLazy<bool>(() => this.LoadTimestamps());
        }

        public async Task DownloadChanges(Uri requestUri, IHttp http)
        {
            //get latest known timestamp for a reqeust
            string timestamp = await GetLastTimestampForRequest(requestUri);
            Uri stampedRequestUri;
            if (timestamp != null)
            {
                stampedRequestUri = new Uri(string.Format("{0}&version={1}", requestUri.OriginalString, Uri.EscapeDataString(timestamp)));
            }
            else
            {
                stampedRequestUri = requestUri;
            }

            //send the timestamped request
            http.OriginalRequest.RequestUri = stampedRequestUri;
            HttpResponseMessage response = await http.SendOriginalAsync();
            JObject json = await ResponseHelper.GetResponseAsJObject(response.Content);

            // get the tablename out of the uri
            string tableName = UriHelper.GetTableNameFromUri(requestUri);

            using (await this.storage.Open())
            {
                JToken newtimestamp;
                if (json.TryGetValue("__version", out newtimestamp))
                {
                    await this.SetLastTimestampForRequest(requestUri, newtimestamp.ToString());
                }

                await this.storage.StoreData(tableName, ResponseHelper.GetResultsJArrayFromJson(json));
                await this.storage.RemoveStoredData(tableName, ResponseHelper.GetDeletedJArrayFromJson(json));
            }
        }

        /// <summary>
        /// Uploads changes using the specified table URI. This Uri should end with /table/tablename
        /// </summary>
        /// <param name="tableUri">The table URI.</param>
        /// <param name="getResponse">The get response.</param>
        /// <returns></returns>
        public async Task UploadChanges(Uri tableUri, IHttp http)
        {
            Contract.Requires<ArgumentNullException>(tableUri != null, "tableUri");
            Contract.Requires<ArgumentNullException>(http != null, "http");

            // return is there is nothing to sync
            if (!hasLocalChanges)
            {
                return;
            }

            string tableName = UriHelper.GetTableNameFromUri(tableUri);

            // all communication with the database should be on the same thread
            using (await this.storage.Open())
            {
                //sent all local changes 
                JArray localChanges = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
                foreach (JObject item in localChanges)
                {
                    try
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
                                    await this.UploadInsert(item, tableUri, http);
                                    break;
                                case ItemStatus.Changed:
                                    await this.UploadUpdate(item, tableUri, http);
                                    break;
                                case ItemStatus.Deleted:
                                    await this.UploadDelete(item, tableUri, http);
                                    break;
                            };
                        }
                    }
                    catch { }
                }

                //we have synchronized everything
                this.hasLocalChanges = false;
            }
        }

        public async Task<JObject> UploadInsert(JObject item, Uri tableUri, IHttp http)
        {
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Post, tableUri);
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            JObject response = await http.GetJsonAsync(req);
            JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
            IEnumerable<string> deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

            string tableName = UriHelper.GetTableNameFromUri(tableUri);

            await this.storage.StoreData(tableName, results);
            await this.storage.RemoveStoredData(tableName, deleted);

            return response;
        }

        public async Task<JObject> UploadUpdate(JObject item, Uri tableUri, IHttp http)
        {
            string version = item["__version"].ToString();
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            Uri updateUri = new Uri(tableUri.OriginalString + "/" + item["id"].ToString());

            HttpRequestMessage req = http.CreateRequest(new HttpMethod("PATCH"), updateUri, new Dictionary<string, string>() { { "If-Match", string.Format("\"{0}\"", version) } });
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            try
            {
                JObject response = await http.GetJsonAsync(req);
                JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
                IEnumerable<string> deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

                string tableName = UriHelper.GetTableNameFromUri(tableUri);

                await this.storage.StoreData(tableName, results);
                await this.storage.RemoveStoredData(tableName, deleted);

                return response;
            }
            catch(HttpStatusCodeException e)
            {
                if(e.StatusCode == HttpStatusCode.Conflict)
                {
                    Conflict c = JsonConvert.DeserializeObject<Conflict>(e.Message);
                    if(c != null)
                    {
                        this.OnConflict(c);
                        return ResponseHelper.CreateSyncResponseWithItems(c.CurrentItem, null);
                    }
                }
                return ResponseHelper.CreateSyncResponseWithItems(null, null);
            }
        }

        public async Task<JObject> UploadDelete(JObject item, Uri tableUri, IHttp http)
        {
            Uri deleteUri = new Uri(tableUri.OriginalString + "/" + item["id"].ToString());

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Delete, deleteUri);

            try
            {
                JObject response = await http.GetJsonAsync(req);
                JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
                IEnumerable<string> deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

                string tableName = UriHelper.GetTableNameFromUri(tableUri);

                await this.storage.StoreData(tableName, results);
                await this.storage.RemoveStoredData(tableName, deleted);

                return response;
            }
            catch (HttpStatusCodeException e)
            {
                if (e.StatusCode == HttpStatusCode.Conflict)
                {
                    Conflict c = JsonConvert.DeserializeObject<Conflict>(e.Message);
                    if (c != null)
                    {
                        this.OnConflict(c);
                        return ResponseHelper.CreateSyncResponseWithItems(null, c.CurrentItem["id"]);
                    }
                }
                return ResponseHelper.CreateSyncResponseWithItems(null, null);
            }
        }

        public void NotifyOfUnsynchronizedChange()
        {
            hasLocalChanges = true;
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

        private async Task<bool> LoadTimestamps()
        {
            using (await this.storage.Open())
            {
                JArray knownUris = await this.storage.GetStoredData("timestamp_requests", new StaticQueryOptions());

                timestamps = new Dictionary<string, Tuple<string, string>>();
                foreach (var uri in knownUris)
                {
                    this.timestamps.Add(uri["requesturl"].ToString(), Tuple.Create(uri["id"].ToString(), uri["__version"].ToString()));
                }
            }
            return true;
        }

        private async Task<string> GetLastTimestampForRequest(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            //make sure the timestamps are loaded
            await this.timestampsLoaded;

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
