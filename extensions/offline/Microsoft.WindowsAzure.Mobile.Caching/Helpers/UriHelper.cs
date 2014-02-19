using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    internal class UriHelper
    {
        public static string GetIdFromUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string path = requestUri.AbsolutePath.Trim('/');
            int lastSlash = path.LastIndexOf('/');
            return path.Substring(lastSlash + 1);
        }

        public static string GetTableNameFromUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string tables = "tables/";
            string path = requestUri.AbsolutePath.Trim('/');
            int startIndex = path.IndexOf(tables) + tables.Length;
            int endIndex = path.IndexOf('/', startIndex);
            if (endIndex == -1)
            {
                endIndex = path.Length;
            }
            return path.Substring(startIndex, endIndex - startIndex);
        }

        public static Uri GetCleanTableUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string tables = "tables/";
            string path = requestUri.AbsolutePath.Trim('/');
            int startIndex = path.IndexOf(tables) + tables.Length;
            int endIndex = path.IndexOf('/', startIndex);
            if (endIndex == -1)
            {
                endIndex = path.Length;
            }
            return new Uri(string.Format("{0}://{1}/{2}", requestUri.Scheme, requestUri.Host, path.Substring(0, endIndex)));
        }

        public static IDictionary<string, string> GetQueryParameters(Uri requestUri)
        {
            IDictionary<string, string> parameters = new Dictionary<string, string>();
            foreach(var kvp in requestUri.GetQueryNameValuePairs().Where(i => !i.Key.StartsWith("$")))
            {
                parameters.Add(kvp);
            }
            return parameters;
        }
    }
}
