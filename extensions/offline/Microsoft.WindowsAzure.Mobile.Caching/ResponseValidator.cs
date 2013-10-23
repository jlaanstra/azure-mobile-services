using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ResponseValidator
    {
        public static void EnsureValidInsertResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("results", out value) || !(value is JArray))
            {
                throw new InvalidOperationException("Invalid insert response.");
            }
        }

        public static void EnsureValidReadResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("results", out value) || !(value is JArray)
                || !json.TryGetValue("deleted", out value) || !(value is JArray) 
                || !json.TryGetValue("timestamp", out value))
            {
                throw new InvalidOperationException("Invalid read response.");
            }
        }

        public static void EnsureValidUpdateResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("results", out value) || !(value is JArray))
            {
                throw new InvalidOperationException("Invalid update response.");
            }
        }

        public static void EnsureValidDeleteResponse(JObject json)
        {
            JToken value;
            if (!json.TryGetValue("deleted", out value) || !(value is JArray))
            {
                throw new InvalidOperationException("Invalid delete response.");
            }
        }
    }
}
