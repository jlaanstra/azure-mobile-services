using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ResponseHelper
    {
        public static void EnsureValidSyncResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("results", out value) || !(value is JArray)
                || !json.TryGetValue("deleted", out value) || !(value is JArray)
                || !json.TryGetValue("timestamp", out value))
            {
                throw new InvalidOperationException("Invalid sync response.");
            }
        }

        public static async Task<HttpContent> TransformSyncReadResponse(HttpContent syncContent)
        {
            string rawContent = await syncContent.ReadAsStringAsync();
            JObject jobject = JObject.Parse(rawContent);
            EnsureValidSyncResponse(jobject);
            jobject.Remove("deleted");
            jobject.Remove("timestamp");

            return new StringContent(jobject.ToString());
        }

        public static async Task<HttpContent> TransformSyncInsertResponse(HttpContent syncContent)
        {
            string rawContent = await syncContent.ReadAsStringAsync();
            JObject jobject = JObject.Parse(rawContent);
            EnsureValidSyncResponse(jobject);
            jobject.Remove("deleted");
            jobject.Remove("timestamp");

            return new StringContent(((JArray)jobject["results"]).First.ToString());
        }

        public static async Task<HttpContent> TransformSyncUpdateResponse(HttpContent syncContent)
        {
            string rawContent = await syncContent.ReadAsStringAsync();
            JObject jobject = JObject.Parse(rawContent);
            EnsureValidSyncResponse(jobject);
            jobject.Remove("deleted");
            jobject.Remove("timestamp");

            return new StringContent(((JArray)jobject["results"]).First.ToString());
        }

        public static Task<HttpContent> TransformSyncDeleteResponse(HttpContent syncContent)
        {
            return Task.FromResult<HttpContent>(new StringContent(string.Empty));
        }
    }
}
