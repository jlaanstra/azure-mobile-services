using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public static class IStructuredStorageExtensions
    {
        public static Task<JArray> GetItemsToSynchronize(this IStructuredStorage This, string tableName)
        {
            if(tableName == null)
            {
                throw new ArgumentNullException("tableName");
            }

            return This.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery("status ne 0") });
        }

        public static Task<JObject> GetItemWithId(this IStructuredStorage This, string tableName, string id)
        {
            return This.GetStoredData(tableName, new StaticQueryOptions() { Filter = new FilterQuery(string.Format("id eq '{0}'", Uri.EscapeDataString(id.Replace("'", "''")))) })
                .ContinueWith(t => t.Result.OfType<JObject>().FirstOrDefault(), TaskContinuationOptions.OnlyOnRanToCompletion);
        }
    }
}
