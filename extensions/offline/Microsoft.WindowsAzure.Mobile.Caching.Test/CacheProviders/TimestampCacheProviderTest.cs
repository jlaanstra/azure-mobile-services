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
            this.synchronizer.Setup(s => s.UploadChanges(It.IsAny<Uri>(), It.IsAny<IHttp>())).Returns(Task.FromResult(0));
        }        

        [TestMethod]
        public async Task ReadShouldPassResultsAndRemoveEverythingElse()
        {
            ICacheProvider provider = new TimestampCacheProvider(this.storage.Object, this.network.Object, this.synchronizer.Object, null);
            string url = "http://localhost/tables/table/";
            string jsonObject = @"{
                                ""id"": 1,
                                ""__version"": ""00000000"",
                                ""text"": ""text""
                            }";

            #region Setup

            this.network.Setup(ini => ini.IsConnectedToInternet()).Returns(() => Task.FromResult(true));

            this.synchronizer.Setup(s => s.DownloadChanges(It.Is<Uri>(u => u.OriginalString.Equals(url)), It.Is<IHttp>(h => h == this.http.Object))).Returns(Task.FromResult(new JObject()));
            this.synchronizer.Setup(s => s.UploadChanges(It.Is<Uri>(u => u.OriginalString.Equals(url)), It.Is<IHttp>(h => h == this.http.Object))).Returns(Task.FromResult(0));
            
            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("table")), It.IsAny<IQueryOptions>()))
               .Returns(() =>
               {
                   return Task.FromResult(new JArray(JObject.Parse(jsonObject)));
               });
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = new StringContent(string.Format(
                @"{{
                    ""results"": [ {0} ],
                    ""deleted"": [ ""B1A60844-236A-43EA-851F-7DCD7D5755FA"" ],
                    ""__version"": ""00000001""
                }}", jsonObject));
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            #endregion

            HttpContent content = await provider.Read(new Uri(url),this.http.Object);

            IDictionary<string, JToken> obj = JObject.Parse(await content.ReadAsStringAsync());

            Assert.IsTrue(obj.ContainsKey("results"));
            Assert.IsFalse(obj.ContainsKey("deleted"));
            Assert.IsFalse(obj.ContainsKey("__version"));

            var results = obj["results"] as JArray;
            var result = results.First as IDictionary<string, JToken>;

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ContainsKey("id"));
            Assert.IsTrue(result.ContainsKey("text"));
            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual(new Uri(url), request.RequestUri);
        }
    }

    class TestStructuredStorage : IStructuredStorage
    {
        public JArray StoredArray { get; set; }

        public Task<IDisposable> Open()
        {
            return Task.FromResult(Disposable.Empty);
        }

        public Task<JArray> GetStoredData(string tableName, IQueryOptions query)
        {
            return Task.FromResult(StoredArray);
        }

        public Task StoreData(string tableName, JArray data, bool overwrite = true)
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
