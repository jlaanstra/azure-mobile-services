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

            string path = requestUri.AbsolutePath;
            int lastSlash = path.LastIndexOf('/');
            return path.Substring(lastSlash + 1);
        }

        public static string GetTableNameFromUri(Uri requestUri)
        {
            Contract.Requires<ArgumentNullException>(requestUri != null, "requestUri");

            string tables = "/tables/";
            string path = requestUri.AbsolutePath;
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

            string tables = "/tables/";
            string url = requestUri.OriginalString;
            //remove query
            url = url.Split('?').First();
            int endIndex = url.IndexOf('/', url.IndexOf(tables) + tables.Length);
            if (endIndex == -1)
            {
                endIndex = url.Length;
            }
            return new Uri(url.Substring(0, endIndex));
        }
    }
}
