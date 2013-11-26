using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class Synchronizer : ISynchronizer
    {
        private readonly IStructuredStorage storage;

        /// <summary>
        /// We are going to be smart by using this value.
        /// </summary>
        private bool hasLocalChanges = true;

        public Synchronizer(IStructuredStorage storage)
        {
            Contract.Requires<ArgumentNullException>(storage != null, "storage");
        }

        /// <summary>
        /// Synchronizes changes using the specified table URI. This Uri should end with /table/tablename
        /// </summary>
        /// <param name="tableUri">The table URI.</param>
        /// <param name="getResponse">The get response.</param>
        /// <returns></returns>
        public async Task Synchronize(string tableName)
        {
            Contract.Requires<ArgumentNullException>(tableName != null, "tableName");

            //return is there is nothing to sync
            if (!hasLocalChanges)
            {
                return;
            }

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
                                await this.SynchronizeInsert(item, tableName);
                                break;
                            case ItemStatus.Changed:
                                await this.SynchronizeUpdate(item, tableName);
                                break;
                            case ItemStatus.Deleted:
                                await this.SynchronizeDelete(item, tableName);
                                break;
                        };
                    }
                }

                //we have synchronized everything
                this.hasLocalChanges = false;
            }
        }

        private async Task SynchronizeInsert(JObject item, string tableName)
        {
            await this.MobileServiceClient.GetTable(tableName).InsertAsync(item, new Dictionary<string, string>() { { "sync", "1" } });

            await this.storage.StoreData(tableName, new JArray(item));
        }

        private async Task SynchronizeUpdate(JObject item, string tableName)
        {
            await this.MobileServiceClient.GetTable(tableName).UpdateAsync(item, new Dictionary<string, string>() { { "sync", "1" } });

            await this.storage.UpdateData(tableName, new JArray(item));
        }

        private async Task SynchronizeDelete(JObject item, string tableName)
        {

        }

        public void NotifyOfUnsynchronizedChange()
        {
            hasLocalChanges = true;
        }

        public IMobileServiceClient MobileServiceClient { get; set; }
    }
}
