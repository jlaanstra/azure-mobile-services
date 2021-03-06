﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface IStructuredStorage
    {
        Task<IDisposable> Open();

        Task<JArray> GetStoredData(string tableName, IQueryOptions query);

        Task StoreData(string tableName, JArray data, bool overwrite = true);

        Task UpdateData(string tableName, JArray data);

        Task RemoveStoredData(string tableName, JArray ids);

        Task Purge();
    }
}
