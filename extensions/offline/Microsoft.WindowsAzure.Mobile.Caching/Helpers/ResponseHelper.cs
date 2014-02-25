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
                // result should be an array of a items
                // insert them as an unchanged items
                dataForInsertion = (JArray)results;
                foreach (JObject item in dataForInsertion.OfType<JObject>())
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

        public static JArray GetDeletedJArrayFromJson(JObject json)
        {
            JArray dataForInsertion;

            JToken deleted;
            if (json.TryGetValue("deleted", out deleted) && deleted is JArray)
            {
                dataForInsertion = (JArray)deleted;
            }
            else
            {
                dataForInsertion = new JArray();
            }

            return dataForInsertion;
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

        public static JObject CreateSyncResponseWithItems(JToken result, JToken deleted)
        {
            JObject resp = new JObject();
            resp.Add("__version", new JValue(string.Empty));
            resp.Add("results", result != null ? new JArray(result) : new JArray());
            resp.Add("deleted", deleted != null ? new JArray(deleted) : new JArray());

            return resp;
        }

        public static JObject MergeResponses(JObject current, JObject response)
        {
            var currResults = ResponseHelper.GetResultsJArrayFromJson(current);
            var currDeletes = ResponseHelper.GetDeletedJArrayFromJson(current);
            foreach (var obj in ResponseHelper.GetResultsJArrayFromJson(response))
            {
                currResults.Add(obj);
            }
            foreach (var obj in ResponseHelper.GetDeletedJArrayFromJson(response))
            {
                currDeletes.Add(obj);
            }

            return current;
        }
    }
}
