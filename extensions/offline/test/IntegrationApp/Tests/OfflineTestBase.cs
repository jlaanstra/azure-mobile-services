// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ----------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Caching;
using Microsoft.WindowsAzure.MobileServices.TestFramework;

namespace Microsoft.WindowsAzure.MobileServices.Test
{
    /// <summary>
    /// Base class for functional tests.
    /// </summary>
    public class OfflineTestBase : TestBase
    {
        /// <summary>
        /// Get a client pointed at the test server without request logging.
        /// </summary>
        /// <returns>The test client.</returns>
        public override Task Initialize()
        {
            string offlineRuntimeUrl = this.GetTestSetting("MobileServiceOfflineRuntimeUrl");
            string offlineRuntimeKey = this.GetTestSetting("MobileServiceOfflineRuntimeKey");
            string normalRuntimeUrl = this.GetTestSetting("MobileServiceNormalRuntimeUrl");
            string normalRuntimeKey = this.GetTestSetting("MobileServiceNormalRuntimeKey");

            this.Storage = new SQLiteStructuredStorage("cache.db");
            ToggableNetworkInformation network = new ToggableNetworkInformation();
            network.IsOnline = true;
            this.NetworkInformation = network;
            ISynchronizer synchronizer = new TimestampSynchronizer(this.Storage);
            this.CacheProvider = new TimestampCacheProvider(this.Storage, network, synchronizer, this.AreWeCachingThis);

            this.OfflineClient = new MobileServiceClient(offlineRuntimeUrl, offlineRuntimeKey, new LoggingHttpHandler(this), new CacheHandler(this.CacheProvider));

            this.NormalClient = new MobileServiceClient(normalRuntimeUrl, normalRuntimeKey, new LoggingHttpHandler(this));

            return Task.FromResult(0);
        }

        public MobileServiceClient OfflineClient { get; private set; }

        public MobileServiceClient NormalClient { get; private set; }

        public ToggableNetworkInformation NetworkInformation { get; private set; }

        public ICacheProvider CacheProvider { get; private set; }

        public IStructuredStorage Storage { get; private set; }

        protected virtual bool AreWeCachingThis(Uri uri)
        {
            return true;
        }
    }

    class LoggingHttpHandler : DelegatingHandler
    {
        public TestBase Test { get; private set; }

        public LoggingHttpHandler(TestBase test)
        {
            Test = test;
        }

        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Test.Log("    >>> {0} {1} {2}", request.Method, request.RequestUri, request.Content);
            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
            Test.Log("    <<< {0} {1} {2}", response.StatusCode, response.ReasonPhrase, response.Content);
            return response;
        }
    }
}
