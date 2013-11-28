using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Diagnostics;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    internal class ResponseHelper
    {
        public static async Task<JObject> GetResponseAsJObject(HttpContent response)
        {
            string rawContent = await response.ReadAsStringAsync();
            JObject json = JObject.Parse(rawContent);
            ResponseHelper.EnsureValidSyncResponse(json);
            Debug.WriteLine("Reponse returned:    {0}", json);
            return json;
        }

        public static JArray GetResultsJArrayFromJson(JObject json)
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

        public static IEnumerable<string> GetDeletedJArrayFromJson(JObject json)
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

        public static void EnsureValidSyncResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("results", out value) || !(value is JArray)
                || !json.TryGetValue("deleted", out value) || !(value is JArray)
                || !json.TryGetValue("__version", out value))
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
            jobject.Remove("__version");

            return new StringContent(jobject.ToString());
        }

        public static async Task<HttpContent> TransformSyncInsertResponse(HttpContent syncContent)
        {
            string rawContent = await syncContent.ReadAsStringAsync();
            JObject jobject = JObject.Parse(rawContent);
            EnsureValidSyncResponse(jobject);
            jobject.Remove("deleted");
            jobject.Remove("__version");

            return new StringContent(((JArray)jobject["results"]).First.ToString());
        }

        public static async Task<HttpContent> TransformSyncUpdateResponse(HttpContent syncContent)
        {
            string rawContent = await syncContent.ReadAsStringAsync();
            JObject jobject = JObject.Parse(rawContent);
            EnsureValidSyncResponse(jobject);
            jobject.Remove("deleted");
            jobject.Remove("__version");

            return new StringContent(((JArray)jobject["results"]).First.ToString());
        }

        public static Task<HttpContent> TransformSyncDeleteResponse(HttpContent syncContent)
        {
            return Task.FromResult<HttpContent>(new StringContent(string.Empty));
        }
    }
}
