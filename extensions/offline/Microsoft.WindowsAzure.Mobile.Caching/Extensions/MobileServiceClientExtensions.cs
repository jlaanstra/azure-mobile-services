using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public static class MobileServiceClientExtensions
    {
        public static MobileServiceClient UseOfflineDataCapabilitiesForTables(this MobileServiceClient This, INetworkInformation networkInfo, IConflictResolver localConflictResolver = null)
        {
            IStructuredStorage storage = new SQLiteStructuredStorage("Microsoft.WindowsAzure.Mobile.Caching.db");
            ISynchronizer synchronizer = new TimestampSynchronizer(storage, localConflictResolver ?? new NoConflictResolver());
            ICacheProvider cacheProvider = new TimestampCacheProvider(storage, networkInfo, synchronizer);

            return new MobileServiceClient(This.ApplicationUri, This.ApplicationKey, new CacheHandler(cacheProvider), new HttpClientHandler());
        }
    }
}
