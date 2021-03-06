﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    /// <summary>
    /// Cache provider that doesn't do caching at all :)
    /// </summary>
    public class DisabledCacheProvider : BaseCacheProvider
    {
        public override Task Purge()
        {
            return Task.FromResult(0);
        }
    }
}
