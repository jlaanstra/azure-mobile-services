using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test.CacheProviders
{
    [TestClass]
    public class TimestampCacheProviderTest
    {
        private TestStructuredStorage storage;
        private TestNetworkInformation network;

        [TestInitialize]
        public void Setup()
        {
            this.storage = new TestStructuredStorage();
            this.network = new TestNetworkInformation();
        }

        [TestMethod]
        public async Task SynchronizeInsertShouldRemoveIdAndTimestamp()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage, this.network, null);

            this.storage.UseArray = new JArray(JObject.Parse(
                                                    @"{
                                                        ""id"": -1,
                                                        ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                                        ""status"": 1,
                                                        ""timestamp"": ""00000000"",
                                                        ""text"": ""text""
                                                    }"
                    ));

            string sendjson = null;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = async (u, c, m) =>
            {
                sendjson = await c.ReadAsStringAsync();
                return new StringContent(string.Empty);
            };

            await provider.Synchronize(new Uri("http://localhost/tables/table/"), getResponse);

            IDictionary<string, JToken> obj = JObject.Parse(sendjson);

            Assert.IsFalse(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsFalse(obj.ContainsKey("timestamp"));
        }

        [TestMethod]
        public async Task SynchronizeDeleteShouldAddIdToUrlAndSendEmptyBody()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage, this.network, null);

            this.storage.UseArray = new JArray(JObject.Parse(
                                                    @"{
                                                        ""id"": 1,
                                                        ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                                        ""status"": 3,
                                                        ""timestamp"": ""00000000"",
                                                        ""text"": ""text""
                                                    }"
                    ));

            HttpContent content = null;
            string url = null;
            HttpMethod method = null;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = async (u, c, m) =>
            {
                url = u.OriginalString;
                content = c;
                method = m;
                return new StringContent(string.Empty);
            };

            await provider.Synchronize(new Uri("http://localhost/tables/table/"), getResponse);

            Assert.IsNull(content);
            Assert.AreEqual(HttpMethod.Delete, method);
            Assert.IsTrue(url.Last() == '1');
        }

        [TestMethod]
        public async Task SynchronizeUpdateShouldKeepIdAndAddToUrl()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage, this.network, null);

            this.storage.UseArray = new JArray(JObject.Parse(
                                                    @"{
                                                        ""id"": 1,
                                                        ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                                        ""status"":2,
                                                        ""timestamp"": ""00000000"",
                                                        ""text"": ""text""
                                                    }"
                    ));

            string sendjson = null;
            string url = null;
            HttpMethod method = null;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = async (u, c, m) =>
            {
                url = u.OriginalString;
                sendjson = await c.ReadAsStringAsync();
                method = m;
                return new StringContent(string.Empty);
            };

            await provider.Synchronize(new Uri("http://localhost/tables/table/"), getResponse);

            IDictionary<string, JToken> obj = JObject.Parse(sendjson);

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsTrue(obj.ContainsKey("timestamp"));
            Assert.AreEqual(new HttpMethod("PATCH"), method);
            Assert.IsTrue(url.Last() == '1');
        }
    }

    class TestStructuredStorage : IStructuredStorage
    {
        public JArray UseArray { get; set; }

        public Task<IDisposable> OpenAsync()
        {
            return Task.FromResult(Disposable.Empty);
        }

        public Task<JArray> GetStoredData(string tableName, IQueryOptions query)
        {
            return Task.FromResult(UseArray);
        }

        public Task StoreData(string tableName, JArray data)
        {
            throw new NotImplementedException();
        }

        public Task UpdateData(string tableName, JArray data)
        {
            throw new NotImplementedException();
        }

        public Task RemoveStoredData(string tableName, IEnumerable<string> guids)
        {
            throw new NotImplementedException();
        }
    }

    class TestNetworkInformation : INetworkInformation
    {
        bool Connected { get; set; }

        public async Task<bool> IsConnectedToInternet()
        {
            return Connected;
        }
    }

}
