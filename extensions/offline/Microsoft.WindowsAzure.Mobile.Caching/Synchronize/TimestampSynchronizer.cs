using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly IConflictResolver conflictResolver;
        private bool hasLocalChanges = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="TimestampSynchronizer"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        public TimestampSynchronizer(IStructuredStorage storage, IConflictResolver conflictResolver)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");
            Contract.Requires<ArgumentNullException>(conflictResolver != null, "conflictResolver");

            this.storage = storage;
            this.conflictResolver = conflictResolver;
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
        public async Task<JObject> UploadChanges(Uri tableUri, IHttp http, JObject item = null, IDictionary<string, string> parameters = null)
        {
            Contract.Requires<ArgumentNullException>(tableUri != null, "tableUri");
            Contract.Requires<ArgumentNullException>(http != null, "http");
            Contract.Requires<ArgumentException>(
                item == null || ((IDictionary<string, JToken>)item).ContainsKey("status"), "item must have status property.");
            Contract.Requires<ArgumentException>(
                item == null || ((IDictionary<string, JToken>)item).ContainsKey("id"), "item must have id property.");
            
            string tableName = UriHelper.GetTableNameFromUri(tableUri);

            if(item == null && !hasLocalChanges)
            {
                return new JObject();
            }

            IEnumerable<JToken> localChanges = new JArray();
            // all communication with the database should be on the same thread
            using (await this.storage.Open())
            {
                //sent all local changes 
                localChanges = await this.storage.GetItemsToSynchronize(tableName);
            }
            if(item != null)
            {
                string id = item.Value<string>("id");
                localChanges = new JObject[] { item }.Concat(localChanges.Where(t => !t.Value<string>("id").Equals(id)));
            }

            List<ResolvedConflict> conflicts = new List<ResolvedConflict>();

            JObject response = new JObject();
            response.Add("results", new JArray());
            response.Add("deleted", new JArray());

            foreach (JObject change in localChanges)
            {
                Func<JObject,Task<JObject>> action = null;
                action = async (jObj) =>
                {
                    Conflict conflict = null;
                    try
                    {
                        return await this.ProcessJObject(jObj, tableUri, http, parameters);
                    }
                    catch (HttpStatusCodeException e)
                    {
                        if (e.StatusCode == HttpStatusCode.Conflict)
                        {
                            conflict = JsonConvert.DeserializeObject<Conflict>(e.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }

                    // Conflicts on the client
                    if(conflict != null)
                    {
                        ConflictResult res = await conflictResolver.Resolve(conflict);
                        string currentId = change.Value<string>("id");
                        List<JObject> results = new List<JObject>();
                        foreach(var it in res.NewItems)
                        {
                            it["id"] = Guid.NewGuid().ToString();
                            it.Remove("__version");
                            it["status"] = (int)ItemStatus.Inserted;
                            JObject resp = await action(it);
                            foreach(JObject resultItem in ResponseHelper.GetResultsJArrayFromJson(resp).OfType<JObject>())
                            {
                                results.Add(resultItem);
                            }
                        }
                        foreach (var it in res.ModifiedItems)
                        {
                            it["status"] = (int)ItemStatus.Changed;
                            JObject resp = await action(it);
                            foreach (JObject resultItem in ResponseHelper.GetResultsJArrayFromJson(resp).OfType<JObject>())
                            {
                                results.Add(resultItem);
                            }
                        }
                        foreach (var it in res.DeletedItems)
                        {
                            it["status"] = (int)ItemStatus.Deleted;
                            JObject resp = await action(it);
                            foreach (JObject resultItem in ResponseHelper.GetResultsJArrayFromJson(resp).OfType<JObject>())
                            {
                                results.Add(resultItem);
                            }
                        }
                        conflicts.Add(new ResolvedConflict("client", conflict.Type, results));
                    }
                    return ResponseHelper.CreateSyncResponseWithItems(null, null);
                };

                JObject result = await action(change);
                // check if conflict was resolved on the server
                string resolveStrategy = result.Value<string>("conflictResolved");
                if(resolveStrategy != null)
                {
                    conflicts.Add(new ResolvedConflict(resolveStrategy, (ConflictType)result.Value<int>("conflictType"), ResponseHelper.GetResultsJArrayFromJson(result).OfType<JObject>()));
                }
                response = ResponseHelper.MergeResponses(response, result);
            }

            // if we have conflicts resolved responses might not conform to Mobile Services formats 
            // so we throw to work around.
            if(conflicts.Count > 0)
            {
                throw new MobileServiceConflictsResolvedException(conflicts);
            }

            response.Add("__version", string.Empty);
            return response;
        }

        private async Task<JObject> ProcessJObject(JObject change, Uri tableUri, IHttp http, IDictionary<string, string> parameters)
        {
            JObject result = null;
            JToken status;
            if (change.TryGetValue("status", out status))
            {
                ItemStatus itemStatus = (ItemStatus)(int)status;
                change.Remove("status");
                //preform calls based on status: insert, change, delete 
                switch (itemStatus)
                {
                    case ItemStatus.Inserted:
                        result = await this.UploadInsert(change, tableUri, http, parameters);
                        break;
                    case ItemStatus.Changed:
                        result = await this.UploadUpdate(change, tableUri, http, parameters);
                        break;
                    case ItemStatus.Deleted:
                        result = await this.UploadDelete(change, tableUri, http, parameters);
                        break;
                };
            }

            return result;
        }
        private async Task<JObject> ProcessRequest(HttpRequestMessage req, Uri tableUri, IHttp http)
        {
            JObject response = await http.GetJsonAsync(req);
            JArray results = ResponseHelper.GetResultsJArrayFromJson(response);
            JArray deleted = ResponseHelper.GetDeletedJArrayFromJson(response);
            foreach (JObject jObj in results.OfType<JObject>())
            {
                jObj["status"] = (int)ItemStatus.Unchanged;
            }

            string tableName = UriHelper.GetTableNameFromUri(tableUri);

            using (await this.storage.Open())
            {
                await this.storage.StoreData(tableName, results);
                await this.storage.RemoveStoredData(tableName, deleted);
            }

            foreach (JObject jObj in results.OfType<JObject>())
            {
                jObj.Remove("status");
            }

            return response;
        }

        public Task<JObject> UploadInsert(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            string paramString = TimestampSynchronizer.GetQueryString(parameters);

            Uri insertUri = new Uri(string.Format("{0}{1}",
                tableUri.OriginalString.TrimEnd('/'),
                paramString != null ? "?" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Post, insertUri);
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            return ProcessRequest(req, tableUri, http);
        }

        public Task<JObject> UploadUpdate(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            string version = null;
            JToken versionToken = item["__version"];
            if(versionToken != null)
            {
                version = versionToken.ToString();
            }
            else
            {
                throw new InvalidOperationException("Cannot update value without __version.");
            }

            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            string paramString = TimestampSynchronizer.GetQueryString(parameters);

            Uri updateUri = new Uri(string.Format("{0}/{1}{2}",
                tableUri.OriginalString.TrimEnd('/'),
                Uri.EscapeDataString(item["id"].ToString()),
                paramString != null ? "?" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(new HttpMethod("PATCH"), updateUri, new Dictionary<string, string>() { { "If-Match", string.Format("\"{0}\"", version) } });
            req.Content = new StringContent(insertItem.ToString(Formatting.None), Encoding.UTF8, "application/json");

            return ProcessRequest(req, tableUri, http);            
        }

        public Task<JObject> UploadDelete(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null)
        {
            string paramString = TimestampSynchronizer.GetQueryString(parameters);
            Uri deleteUri = new Uri(string.Format("{0}/{1}?version={2}{3}", 
                tableUri.OriginalString.TrimEnd('/'), 
                Uri.EscapeDataString(item["id"].ToString()), 
                Uri.EscapeDataString(item["__version"].ToString()),
                paramString != null ? "&" + paramString : string.Empty));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Delete, deleteUri);

            return ProcessRequest(req, tableUri, http);
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
