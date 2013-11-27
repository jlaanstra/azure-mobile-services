using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class Synchronizer : ISynchronizer
    {
        private readonly IStructuredStorage storage;

        /// <summary>
        /// We are going to be smart by using this value.
        /// </summary>
        /// <param name="storage">The storage.</param>
        private bool hasLocalChanges = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="Synchronizer"/> class.
        /// </summary>
        /// <param name="storage">The storage.</param>
        public Synchronizer(IStructuredStorage storage)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");

            this.storage = storage;
        }

        /// <summary>
        /// Synchronizes changes using the specified table URI. This Uri should end with /table/tablename
        /// </summary>
        /// <param name="tableUri">The table URI.</param>
        /// <param name="getResponse">The get response.</param>
        /// <returns></returns>
        public async Task Synchronize(Uri tableUri, IHttp http)
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
            using (await this.storage.OpenAsync())
            {
                //sent all local changes 
                JArray localChanges = await this.storage.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
                foreach (JObject item in localChanges)
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
                                await this.SynchronizeInsert(item, tableUri, tableName, http);
                                break;
                            case ItemStatus.Changed:
                                await this.SynchronizeUpdate(item, tableUri, tableName, http);
                                break;
                            case ItemStatus.Deleted:
                                await this.SynchronizeDelete(item, tableUri, tableName, http);
                                break;
                        };
                    }
                }

                //we have synchronized everything
                this.hasLocalChanges = false;
            }
        }

        private async Task SynchronizeInsert(JObject item, Uri tableUri, string tableName, IHttp http)
        {
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Post, tableUri);
            req.Content = new StringContent(insertItem.ToString(Formatting.None));

            JObject response = await http.GetJsonAsync(req);
            JArray results = ResponseHelper.GetResultsJArrayFromJson(response);

            await this.storage.StoreData(tableName, results);
        }

        private async Task SynchronizeUpdate(JObject item, Uri tableUri, string tableName, IHttp http)
        {
            string version = item["__version"].ToString();
            //remove systemproperties
            JObject insertItem = item.Remove(prop => prop.Name.StartsWith("__"));

            Uri updateUri = new Uri(tableUri.OriginalString + "/" + item["id"].ToString());

            HttpRequestMessage req = http.CreateRequest(new HttpMethod("PATCH"), updateUri, new Dictionary<string, string>() { { "If-Match", string.Format("\"{0}\"", version) } });
            req.Content = new StringContent(insertItem.ToString(Formatting.None));

            JObject response = await http.GetJsonAsync(req);
            JArray results = ResponseHelper.GetResultsJArrayFromJson(response);

            await this.storage.UpdateData(tableName, results);
        }

        private async Task SynchronizeDelete(JObject item, Uri tableUri, string tableName, IHttp http)
        {
            Uri deleteUri = new Uri(tableUri.OriginalString + "/" + item["id"].ToString());

            HttpRequestMessage req = http.CreateRequest(HttpMethod.Delete, deleteUri);

            JObject response = await http.GetJsonAsync(req);
            IEnumerable<string> deleted = ResponseHelper.GetDeletedJArrayFromJson(response);

            await this.storage.RemoveStoredData(tableName, deleted);
        }

        public void NotifyOfUnsynchronizedChange()
        {
            hasLocalChanges = true;
        }
    }
}
