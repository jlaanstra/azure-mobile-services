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
    public class TimestampCacheProviderTestOffline
    {
        private Mock<IStructuredStorage> storage;
        private Mock<INetworkInformation> network;
        private Mock<IHttp> http;
        private Mock<ISynchronizer> synchronizer;

        [TestInitialize]
        public void Setup()
        {
            this.storage = new Mock<IStructuredStorage>();
            this.storage.Setup(iss => iss.Open()).Returns(() => Task.FromResult(Disposable.Empty));
            this.network = new Mock<INetworkInformation>();
            this.network.Setup(ini => ini.IsConnectedToInternet()).Returns(() => Task.FromResult(false));
            this.http = new Mock<IHttp>();
            this.synchronizer = new Mock<ISynchronizer>();
        }        

        [TestMethod]
        public async Task ReadShouldExecuteQueryLocally()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/";
            string jsonObject = @"{
                                ""id"": ""1"",
                                ""__version"": ""00000000"",
                                ""text"": ""text"",
                                ""status"": 0
                            }";

            #region Setup

            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.IsAny<IQueryOptions>()))
               .Returns(() =>
               {
                   return Task.FromResult(new JArray(JObject.Parse(jsonObject)));
               });
           
            #endregion

            HttpContent content = await provider.Read(new Uri(url),this.http.Object);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("results"));
            Assert.IsTrue(obj.ContainsKey("count"));
            Assert.IsFalse(obj.ContainsKey("deleted"));
            Assert.IsFalse(obj.ContainsKey("__version"));

            var results = obj["results"] as JArray;
            var result = results.First;

            Assert.IsNotNull(result);
            Assert.AreEqual("1", result.Value<string>("id"));
            Assert.AreEqual("text", result.Value<string>("text"));
        }

        [TestMethod]
        public async Task InsertStoresItemLocally()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table";
            string jsonObject = @"{
                                ""id"": 1,
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";

            #region Setup

            this.storage.Setup(iss => iss.StoreData(It.Is<string>(str => str.Equals("table")), It.IsAny<JArray>(), It.IsAny<bool>()))
               .Returns(() =>
               {
                   return Task.FromResult(0);
               });            

            #endregion

            HttpContent content = await provider.Insert(new Uri(url), new StringContent(jsonObject), this.http.Object);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsTrue(obj.ContainsKey("__version"));
            Assert.IsTrue(obj.ContainsKey("text"));
            Assert.IsFalse(obj.ContainsKey("status"));
        }

        [TestMethod]
        public async Task UpdateStoresExistingInsertAsInsertLocallyTest()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/1";
            string jsonObject = @"{
                                ""id"": 1,
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";
            JObject resp = new JObject();
            resp.Add("results", new JArray(JObject.Parse(jsonObject)));

            #region Setup

            this.storage.Setup(iss => iss.UpdateData(It.Is<string>(str => str.Equals("table")), It.Is<JArray>(arr => arr.First.Value<int>("status") == (int)ItemStatus.Inserted)))
               .Returns(() =>
               {
                   return Task.FromResult(0);
               });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.Is<IQueryOptions>(q => q.Filter != null && q.Filter.RawValue.Equals("id eq '1'"))))
                .Returns(() => 
                {
                    JObject jObj = JObject.Parse(jsonObject);
                    jObj["status"] = (int)ItemStatus.Inserted;
                    return Task.FromResult(new JArray(jObj));
                });

            #endregion

            HttpContent content = await provider.Update(new Uri(url), new StringContent(jsonObject), this.http.Object);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsTrue(obj.ContainsKey("__version"));
            Assert.IsTrue(obj.ContainsKey("text"));
            Assert.IsFalse(obj.ContainsKey("status"));
        }

        [TestMethod]
        public async Task UpdateStoresUnchangedAsChangedLocallyTest()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/1";
            string jsonObject = @"{
                                ""id"": 1,
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";
            JObject resp = new JObject();
            resp.Add("results", new JArray(JObject.Parse(jsonObject)));

            #region Setup

            this.storage.Setup(iss => iss.UpdateData(It.Is<string>(str => str.Equals("table")), It.Is<JArray>(arr => arr.First.Value<int>("status") == (int)ItemStatus.Changed)))
               .Returns(() =>
               {
                   return Task.FromResult(0);
               });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.Is<IQueryOptions>(q => q.Filter != null && q.Filter.RawValue.Equals("id eq '1'"))))
                .Returns(() =>
                {
                    JObject jObj = JObject.Parse(jsonObject);
                    jObj["status"] = (int)ItemStatus.Unchanged;
                    return Task.FromResult(new JArray(jObj));
                });

            #endregion

            HttpContent content = await provider.Update(new Uri(url), new StringContent(jsonObject), this.http.Object);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsTrue(obj.ContainsKey("__version"));
            Assert.IsTrue(obj.ContainsKey("text"));
            Assert.IsFalse(obj.ContainsKey("status"));
        }

        [TestMethod]
        public async Task DeleteRemovesInsertLocallyTest()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/1";
            string jsonObject = @"{
                                ""id"": ""1"",
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";

            #region Setup

            this.storage.Setup(iss => iss.RemoveStoredData(It.Is<string>(str => str.Equals("table")), It.Is<JArray>(arr => arr.First.ToString() == "1")))
               .Returns(() =>
               {
                   return Task.FromResult(0);
               });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.Is<IQueryOptions>(q => q.Filter != null && q.Filter.RawValue.Equals("id eq '1'"))))
                .Returns(() =>
                {
                    JObject jObj = JObject.Parse(jsonObject);
                    jObj["status"] = (int)ItemStatus.Inserted;
                    return Task.FromResult(new JArray(jObj));
                });
            
            #endregion

            HttpContent content = await provider.Delete(new Uri(url), this.http.Object);

            string result = await content.ReadAsStringAsync();

            Assert.IsTrue(string.IsNullOrEmpty(result));
        }

        [TestMethod]
        public async Task DeleteUpdatesUnchangedItemLocallyTest()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/1";
            string jsonObject = @"{
                                ""id"": ""1"",
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";

            #region Setup

            this.storage.Setup(iss => iss.UpdateData(It.Is<string>(str => str.Equals("table")), It.Is<JArray>(arr => arr.First.Value<int>("status") == (int)ItemStatus.Deleted)))
               .Returns(() =>
               {
                   return Task.FromResult(0);
               });
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.Is<IQueryOptions>(q => q.Filter != null && q.Filter.RawValue.Equals("id eq '1'"))))
                .Returns(() =>
                {
                    JObject jObj = JObject.Parse(jsonObject);
                    jObj["status"] = (int)ItemStatus.Unchanged;
                    return Task.FromResult(new JArray(jObj));
                });

            #endregion

            HttpContent content = await provider.Delete(new Uri(url), this.http.Object);

            string result = await content.ReadAsStringAsync();

            Assert.IsTrue(string.IsNullOrEmpty(result));
        }

        [TestMethod]
        public async Task PurgeTest()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);

            await provider.Purge();

            this.storage.Verify(s => s.Purge(), Times.Once);

            this.storage.Verify();
        }
    }
}
