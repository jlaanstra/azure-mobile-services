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
    public class TimestampSynchronizerTest
    {
        private Mock<IStructuredStorage> storage;
        private Mock<IConflictResolver> conflictResolver;
        private Mock<IHttp> http;

        private string responseContent = @"{{
                    ""results"": [ {0} ],
                    ""deleted"": [ {1} ],
                    ""__version"": ""00000000""
                }}";

        private IDictionary<string, string> parameters = new Dictionary<string, string>() { { "test", "test" } };

        [TestInitialize]
        public void Setup()
        {
            this.storage = new Mock<IStructuredStorage>();
            this.storage.Setup(iss => iss.Open()).Returns(() => Task.FromResult(Disposable.Empty));
            this.storage.Setup(iss => iss.GetStoredData(It.IsAny<string>(), It.IsAny<IQueryOptions>()))
                .Returns(() =>
                {
                    return Task.FromResult(new JArray());
                });
            this.storage.Setup(iss => iss.StoreData(It.IsAny<string>(), It.IsAny<JArray>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.storage.Setup(iss => iss.RemoveStoredData(It.IsAny<string>(), It.IsAny<JArray>()))
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
            this.conflictResolver = new Mock<IConflictResolver>();
            this.conflictResolver.Setup(c => c.Resolve(It.IsAny<Conflict>())).Returns(Task.FromResult(new ConflictResult()));
        }

        [TestMethod]
        public async Task SynchronizeInsertShouldRemoveIdAndTimestamp()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);

            #region Setup
            HttpRequestMessage req = null;

            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Returns<HttpRequestMessage>(async r =>
                {
                    req = r;
                    var sendjson = await r.Content.ReadAsStringAsync();
                    JObject send = JObject.Parse(sendjson);
                    send["__version"] = "00000000";
                    return new HttpResponseMessage() { Content = 
                        new StringContent(string.Format(responseContent, send.ToString(), string.Empty)) };
                });
            #endregion

            JObject jObj = JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 1,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    );

            JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table"), this.http.Object, jObj, parameters);

            Assert.IsNotNull(req);
            Assert.IsNotNull(result.Value<JArray>("results"));

            IDictionary<string, JToken> obj = result.Value<JArray>("results").First as JObject;

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsTrue(obj.ContainsKey("__version"));
            Assert.AreEqual(HttpMethod.Post, req.Method);
        }

        [TestMethod]
        public async Task SynchronizeDeleteShouldAddIdAndVersionToUrlAndSendEmptyBody()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);

            #region Setup
            HttpRequestMessage req = null;

            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Callback<HttpRequestMessage>(r => req = r)
                .Returns<HttpRequestMessage>(request => Task.FromResult(new HttpResponseMessage() { Content = 
                        new StringContent(string.Format(responseContent, string.Empty, string.Format("\"{0}\"", UriHelper.GetIdFromUri(request.RequestUri)))) }));

            #endregion

            JObject jObj = JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 3,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    );

            JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table"), this.http.Object, jObj, parameters);

            Assert.IsNotNull(req);
            HttpContent sendContent = req.Content;
            Assert.IsNull(sendContent);

            Assert.IsNotNull(result.Value<JArray>("deleted"));
            JValue obj = result.Value<JArray>("deleted").First as JValue;
            Assert.AreEqual("B1A60844-236A-43EA-851F-7DCD7D5755FA", obj.ToString());
            Assert.AreEqual(HttpMethod.Delete, req.Method);
            Assert.IsTrue(req.RequestUri.OriginalString.Contains("B1A60844-236A-43EA-851F-7DCD7D5755FA?version=00000000"));
        }

        [TestMethod]
        public async Task SynchronizeUpdateShouldKeepIdAndAddToUrl()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);

            #region Setup
            HttpRequestMessage req = null;

            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Returns<HttpRequestMessage>(async r =>
                {
                    req = r;
                    var sendjson = await r.Content.ReadAsStringAsync();
                    JObject send = JObject.Parse(sendjson);
                    send["__version"] = "00000000";
                    return new HttpResponseMessage()
                    {
                        Content =
                            new StringContent(string.Format(responseContent, send.ToString(), string.Empty))
                    };
                });
            #endregion

            JObject jObj = JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 2,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    );

            JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table/"), this.http.Object, jObj, parameters);

            Assert.IsNotNull(req);
            Assert.IsNotNull(result.Value<JArray>("results"));

            IDictionary<string, JToken> obj = result.Value<JArray>("results").First as JObject;

            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.ContainsKey("id"));
            Assert.IsFalse(obj.ContainsKey("status"));
            Assert.IsTrue(obj.ContainsKey("__version"));
            Assert.AreEqual(new HttpMethod("PATCH"), req.Method);
            Assert.IsTrue(req.RequestUri.OriginalString.Contains("B1A60844-236A-43EA-851F-7DCD7D5755FA"));            
        }

        [TestMethod]
        public async Task DownloadChangesShouldLoadTimestampsWhenOnline()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);
            string url = "http://localhost/tables/table/";
            string timestamp = "00000000";

            #region Setup

            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = new StringContent(
                @"{
                    ""results"": [],
                    ""deleted"": [],
                    ""__version"": ""00000001""
                }");

            this.storage.Setup(iss => iss.GetStoredData(It.Is<string>(str => str.Equals("timestamp_requests")), It.IsAny<IQueryOptions>()))
                .Returns(() =>
                {
                    return Task.FromResult(new JArray(JObject.Parse(string.Format(
                        //escape here
                            @"{{
                                ""requestUri"": ""{0}"",
                                ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                                ""__version"": ""{1}""
                            }}"
                        , url, timestamp))));
                });
            this.storage.Setup(iss => iss.StoreData(It.IsAny<string>(), It.IsAny<JArray>(), It.IsAny<bool>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            this.storage.Setup(iss => iss.RemoveStoredData(It.IsAny<string>(), It.IsAny<JArray>()))
                .Returns(() =>
                {
                    return Task.FromResult(0);
                });
            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            this.http.Setup(h => h.SendAsync(It.Is<HttpRequestMessage>(r => r == request))).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            #endregion

            await synchronizer.DownloadChanges(new Uri(url), this.http.Object);

            Assert.AreEqual(HttpMethod.Get, request.Method);
            Assert.AreEqual(string.Format("{0}&version={1}", url, Uri.EscapeDataString(timestamp)), request.RequestUri.OriginalString);
        }
    }
}
