﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface ISynchronizer
    {
        event EventHandler<Conflict> Conflict;
        void NotifyOfUnsynchronizedChange();

        Task<JObject> DownloadChanges(Uri requestUri, IHttp http);

        Task UploadChanges(Uri tableUri, IHttp http);

        Task<JObject> UploadInsert(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null);

        Task<JObject> UploadUpdate(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null);

        Task<JObject> UploadDelete(JObject item, Uri tableUri, IHttp http, IDictionary<string, string> parameters = null);
    }
}
