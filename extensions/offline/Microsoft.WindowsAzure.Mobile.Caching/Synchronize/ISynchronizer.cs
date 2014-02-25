using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface ISynchronizer
    {
        void NotifyOfUnsynchronizedChange();

        Task<JObject> DownloadChanges(Uri requestUri, IHttp http);

        Task<JObject> UploadChanges(Uri tableUri, IHttp http, JObject item = null, IDictionary<string, string> parameters = null);
    }
}
