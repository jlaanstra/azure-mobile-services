using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class SynchronizerTest
    {
        private Mock<IStructuredStorage> storage;
        private Mock<IHttp> http;

        private HttpContent responseContent = new StringContent(
                @"{
                    ""results"": [],
                    ""deleted"": [],
                    ""__version"": ""00000000""
                }");

        [TestInitialize]
        public void Setup()
        {
            this.storage = new Mock<IStructuredStorage>();
            this.storage.Setup(iss => iss.OpenAsync()).Returns(() => Task.FromResult(Disposable.Empty));
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
            this.storage.Setup(iss => iss.UpdateData(It.IsAny<string>(), It.IsAny<JArray>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.http = new Mock<IHttp>();
            this.http.Setup(h => h.CreateRequest(It.IsAny<HttpMethod>(), It.IsAny<Uri>(), It.IsAny<IDictionary<string, string>>()))
                .Returns<HttpMethod, Uri, IDictionary<string, string>>((m, u, i) =>
                {
                    var req = new HttpRequestMessage(m, u);
                    if (i != null)
                    {
                        foreach (var item in i)
                        {
                            req.Headers.Add(item.Key, item.Value);
                        }
                    }
                    return req;
                });
        }

        [TestMethod]
        public async Task SynchronizeInsertShouldRemoveIdAndTimestamp()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object);

            #region Setup
            HttpRequestMessage req = null;
            string sendjson = null;

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 1,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });
            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Returns<HttpRequestMessage>(async r =>
                {
                    req = r;
                    sendjson = await r.Content.ReadAsStringAsync();
                    return new HttpResponseMessage() { Content = responseContent };
                });
            #endregion

            await synchronizer.UploadChanges(new Uri("http://localhost/tables/table/"), this.http.Object);

            Assert.IsNotNull(req);
            IDictionary<string, JToken> obj = JObject.Parse(sendjson);

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsFalse(obj.ContainsKey("__version"));
            Assert.AreEqual(HttpMethod.Post, req.Method);
        }

        [TestMethod]
        public async Task SynchronizeDeleteShouldAddIdToUrlAndSendEmptyBody()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object);

            #region Setup
            HttpRequestMessage req = null;

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 3,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });
            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Callback<HttpRequestMessage>(r => req = r)
                .Returns(() => Task.FromResult(new HttpResponseMessage() { Content = responseContent }));

            #endregion

            await synchronizer.UploadChanges(new Uri("http://localhost/tables/table/"), this.http.Object);

            Assert.IsNotNull(req);
            HttpContent sendContent = req.Content;

            Assert.IsNull(sendContent);
            Assert.AreEqual(HttpMethod.Delete, req.Method);
            Assert.IsTrue(req.RequestUri.OriginalString.EndsWith("B1A60844-236A-43EA-851F-7DCD7D5755FA"));
        }

        [TestMethod]
        public async Task SynchronizeUpdateShouldKeepIdAndAddToUrl()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object);

            #region Setup
            HttpRequestMessage req = null;
            string sendjson = null;

            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.Is<IQueryOptions>(iqo => iqo.Filter.RawValue.Equals("status ne 0")))).Returns(() =>
            {
                return Task.FromResult(new JArray(JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 2,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    )));
            });
            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Returns<HttpRequestMessage>(async r =>
                {
                    req = r;
                    sendjson = await r.Content.ReadAsStringAsync();
                    return new HttpResponseMessage() { Content = responseContent };
                });
            #endregion

            await synchronizer.UploadChanges(new Uri("http://localhost/tables/table/"), this.http.Object);

            Assert.IsNotNull(req);
            IDictionary<string, JToken> obj = JObject.Parse(sendjson);

            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsFalse(obj.ContainsKey("__version"));
            Assert.AreEqual(new HttpMethod("PATCH"), req.Method);
            Assert.IsTrue(req.RequestUri.OriginalString.EndsWith("B1A60844-236A-43EA-851F-7DCD7D5755FA"));
        }
    }
}
