using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test.CacheProviders
{
    [TestClass]
    public class TimestampCacheProviderTest
    {
        private Mock<IStructuredStorage> storage;
        private Mock<INetworkInformation> network;

        [TestInitialize]
        public void Setup()
        {
            this.storage = new Mock<IStructuredStorage>();
            this.storage.Setup(iss => iss.OpenAsync()).Returns(() => Task.FromResult(Disposable.Empty));
            this.network = new Mock<INetworkInformation>();
            this.network.Setup(ini => ini.IsConnectedToInternet()).Returns(() => Task.FromResult(false));
        }

        [TestMethod]
        public async Task SynchronizeInsertShouldRemoveIdAndTimestamp()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, null);

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": -1,
                            ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 1,
                            ""timestamp"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });

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
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, null);

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": 1,
                            ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 3,
                            ""timestamp"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });

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
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, null);

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": 1,
                            ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 2,
                            ""timestamp"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });

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

        [TestMethod]
        public async Task ReadShouldLoadTimestampsWhenOnline()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, null);
            string url = "http://localhost/tables/table/";
            string timestamp = "00000000";

            #region Setup

            this.network.Setup(ini => ini.IsConnectedToInternet()).Returns(() => Task.FromResult(true));
            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.IsAny<IQueryOptions>()))
               .Returns(() =>
               {
                   return Task.FromResult(new JArray());
               });
            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter != null && iqo.Filter.RawValue.Equals("status ne 0"))))
                .Returns(() =>
                {
                    return Task.FromResult(new JArray());
                });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("timestamp_requests")), It.IsAny<IQueryOptions>()))
                .Returns(() =>
                {
                    return Task.FromResult(new JArray(JObject.Parse(string.Format(
                        //escape here
                            @"{{
                                ""requesturl"": ""{0}"",
                                ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                ""timestamp"": ""{1}""
                            }}"
                        , url, timestamp))));
                });
            this.storage.Setup(iss => iss.StoreData(It.IsAny<string>(), It.IsAny<JArray>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.storage.Setup(iss => iss.RemoveStoredData(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });

            #endregion

            HttpContent sendjson = null;
            string sendurl = null;
            HttpMethod method = null;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = async (u, c, m) =>
            {
                sendurl = u.OriginalString;
                sendjson = c;
                method = m;
                return new StringContent(@"{
                                            ""results"": [],
                                            ""deleted"": [],
                                            ""timestamp"": ""00000001""
                                        }");
            };

            HttpContent content = await provider.Read(new Uri(url), getResponse);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("results"));
            Assert.IsNotNull(obj["results"]);
            Assert.IsNull(sendjson);
            Assert.AreEqual(HttpMethod.Get, method);
            Assert.AreEqual(string.Format("{0}&timestamp={1}", url, Uri.EscapeDataString(timestamp)), sendurl);
        }

        [TestMethod]
        public async Task ReadShouldPassResultsAndRemoveEverythingElse()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, null);
            string url = "http://localhost/tables/table/";
            string jsonObject = @"{
                                ""id"": 1,
                                ""guid"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                ""timestamp"": ""00000000"",
                                ""text"": ""text""
                            }";

            #region Setup

            this.network.Setup(ini => ini.IsConnectedToInternet()).Returns(() => Task.FromResult(true));
            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.IsAny<IQueryOptions>()))
               .Returns(() =>
               {
                   return Task.FromResult(new JArray());
               });
            this.storage.Setup(iss => iss.StoreData(It.IsAny<string>(), It.IsAny<JArray>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.storage.Setup(iss => iss.RemoveStoredData(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.IsAny<IQueryOptions>()))
               .Returns(() =>
               {
                   return Task.FromResult(new JArray(JObject.Parse(jsonObject)));
               });

            #endregion

            HttpContent sendjson = null;
            string sendurl = null;
            HttpMethod method = null;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = async (u, c, m) =>
            {
                sendurl = u.OriginalString;
                sendjson = c;
                method = m;
                return new StringContent(string.Format(@"{{
                                            ""results"": [ {0} ],
                                            ""deleted"": [ ""B1A60844-236A-43EA-851F-7DCD7D5755FA"" ],
                                            ""timestamp"": ""00000001""
                                        }}", jsonObject));
            };

            HttpContent content = await provider.Read(new Uri(url), getResponse);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("results"));
            Assert.IsFalse(obj.ContainsKey("deleted"));
            Assert.IsFalse(obj.ContainsKey("timestamp"));

            var results = obj["results"] as JArray;
            var result = results.First as IDictionary<string, JToken>;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("id"));
            Assert.IsTrue(result.ContainsKey("guid"));
            Assert.IsTrue(result.ContainsKey("text"));
            Assert.IsNull(sendjson);
            Assert.AreEqual(HttpMethod.Get, method);
        }
    }

    class TestStructuredStorage : IStructuredStorage
    {
        public JArray StoredArray { get; set; }

        public Task<IDisposable> OpenAsync()
        {
            return Task.FromResult(Disposable.Empty);
        }

        public Task<JArray> GetStoredData(string tableName, IQueryOptions query)
        {
            return Task.FromResult(StoredArray);
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

}
