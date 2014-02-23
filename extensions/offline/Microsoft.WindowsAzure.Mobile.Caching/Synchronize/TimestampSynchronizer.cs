﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        }

        public async Task<JObject> DownloadChanges(Uri requestUri, IHttp http)
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

            return json;
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

            JArray localChanges = new JArray();
            // all communication with the database should be on the same thread
            using (await this.storage.Open())
            {
                //sent all local changes 
                localChanges = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
            }
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

        public async Task<JObject> UploadInsert(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            string paramString = TimestampSynchronizer.GetQueryString(parameters);

            Uri insertUri = new Uri(string.Format("{0}{1}",
                tableUri.OriginalString,
                paramString != null ? "?" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Post, insertUri);
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            JObject response = await http.GetJsonAsync(req);
            JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
            JArray deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

            string tableName = UriHelper.GetTableNameFromUri(tableUri);

            using (await this.storage.Open())
            {
                await this.storage.StoreData(tableName, results);
                await this.storage.RemoveStoredData(tableName, deleted);
            }

            return response;
        }

        public async Task<JObject> UploadUpdate(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            string version = null;
            JToken versionToken = item["__version"];
            if(versionToken != null)
            {
                version = versionToken.ToString();
            }
            else if(http.OriginalRequest != null)
            {
                EntityTagHeaderValue tag = http.OriginalRequest.Headers.IfMatch.FirstOrDefault();
                if (tag != null)
                {
                    //trim "
                    version = tag.Tag.Trim(new char[] { '"' });
                }
            }

            if(version == null)
            {
                throw new InvalidOperationException("Cannot update value without __version.");
            }

            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            string paramString = TimestampSynchronizer.GetQueryString(parameters);

            Uri updateUri = new Uri(string.Format("{0}/{1}{2}",
                tableUri.OriginalString,
                Uri.EscapeDataString(item["id"].ToString()),
                paramString != null ? "?" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(new HttpMethod("PATCH"), updateUri, new Dictionary<string, string>() { { "If-Match", string.Format("\"{0}\"", version) } });
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            try
            {
                JObject response = await http.GetJsonAsync(req);
                JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
                JArray deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

                string tableName = UriHelper.GetTableNameFromUri(tableUri);

                using (await this.storage.Open())
                {
                    await this.storage.StoreData(tableName, results);
                    await this.storage.RemoveStoredData(tableName, deleted);
                }

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

        public async Task<JObject> UploadDelete(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            string paramString = TimestampSynchronizer.GetQueryString(parameters);
            Uri deleteUri = new Uri(string.Format("{0}/{1}?version={2}{3}", 
                tableUri.OriginalString, 
                Uri.EscapeDataString(item["id"].ToString()), 
                Uri.EscapeDataString(item["__version"].ToString()),
                paramString != null ? "&" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Delete, deleteUri);

            try
            {
                JObject response = await http.GetJsonAsync(req);
                JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
                JArray deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

                string tableName = UriHelper.GetTableNameFromUri(tableUri);

                using (await this.storage.Open())
                {
                    await this.storage.StoreData(tableName, results);
                    await this.storage.RemoveStoredData(tableName, deleted);
                }

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

        private Dictionary<string, string> uriToId = new Dictionary<string,string>();

        private async Task<string> GetLastTimestampForRequest(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            using (await this.storage.Open())
            {
                JArray knownUris = await this.storage.GetStoredData("timestamp_requests", 
                    new StaticQueryOptions() { Filter = new FilterQuery(string.Format("requestUri eq '{0}'", Uri.EscapeDataString(requestUri.OriginalString.Replace("'", "''")))) });

                foreach (var uri in knownUris)
                {
                    this.uriToId[uri.Value<string>("requestUri")] = uri.Value<string>("id");
                    return uri.Value<string>("__version");
                }
                return null;
            }
        }

        private async Task SetLastTimestampForRequest(Uri requestUri, string timestamp)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");
            
            string id;
            if (!this.uriToId.TryGetValue(requestUri.OriginalString, out id))
            {
                //if not exists create new one
                id = Guid.NewGuid().ToString();
            }

            JObject item = new JObject();
            item.Add("id", id);
            // required column so provide default
            item.Add("requestUri", requestUri.OriginalString);
            item.Add("__version", timestamp);
            //timestamps are local only so always unchanged
            item.Add("status", (int)ItemStatus.Unchanged);
            await this.storage.StoreData("timestamp_requests", new JArray(item));

            //after successfull insert add locally
            this.uriToId[requestUri.OriginalString] = id;
        }

        #endregion

        public static string GetQueryString(IDictionary<string, string> parameters)
        {
            string parametersString = null;

            if (parameters != null && parameters.Count > 0)
            {
                parametersString = "";
                string formatString = "{0}={1}";
                foreach (var parameter in parameters)
                {
                    if (parameter.Key.StartsWith("$"))
                    {
                        throw new ArgumentException();
                    }

                    string escapedKey = Uri.EscapeDataString(parameter.Key);
                    string escapedValue = Uri.EscapeDataString(parameter.Value);
                    parametersString += string.Format(CultureInfo.InvariantCulture,
                                                      formatString,
                                                      escapedKey,
                                                      escapedValue);
                    formatString = "&{0}={1}";
                }
            }

            return parametersString;
        }
    }
}
