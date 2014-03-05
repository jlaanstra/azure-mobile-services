using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class TimestampSynchronizerConflictsTest
    {
        private Mock<IStructuredStorage> storage;
        private Mock<IConflictResolver> conflictResolver;
        private Mock<IHttp> http;

        private string responseContent = @"{{
                    ""results"": [ {0} ],
                    ""deleted"": [ {1} ],
                    ""__version"": ""00000000"",
                    ""conflictResolved"": ""lastWriteWins"",
                    ""conflictType"": {2}
                }}";

        private string conflictContent = @"{{
                    ""newItem"": {0},
                    ""currentItem"": {1},
                    ""__version"": ""00000000"",
                    ""conflictType"": {2}
                }}";

        private JObject currentItem = new JObject() 
        { 
            { "id", "B1A60844-236A-43EA-851F-7DCD7D5755FA" },
            { "__version", "00000001"},
            { "text", "text" }
        };

        private JObject newItem = new JObject() 
        { 
            { "id", "B1A60844-236A-43EA-851F-7DCD7D5755FA" },
            { "__version", "00000000"},
            { "text", "newtext" }
        };

        private JObject deletedItem = new JObject() 
        { 
            { "id", "B1A60844-236A-43EA-851F-7DCD7D5755FA" },
            { "__version", "00000000"},
            { "text", "newtext" }
        };

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
        public async Task SynchronizeDeleteConflict()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);

            #region Setup
            HttpRequestMessage req = null;

            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Callback<HttpRequestMessage>(r => req = r)
                .Returns<HttpRequestMessage>(request => Task.FromResult(new HttpResponseMessage() { Content = 
                        new StringContent(string.Format(responseContent, string.Empty, string.Format("\"{0}\"", UriHelper.GetIdFromUri(request.RequestUri)), 1)) }));

            #endregion

            JObject jObj = JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 3,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    );

            MobileServiceConflictsResolvedException ex = null;
            try
            {
                JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table"), this.http.Object, jObj, parameters);
            }
            catch(MobileServiceConflictsResolvedException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.Conflicts.Count() == 1);

            ResolvedConflict conf = ex.Conflicts.First();

            Assert.AreEqual("lastWriteWins", conf.ResolveStrategy);
            Assert.AreEqual(ConflictType.UpdateDelete, conf.Type);
            Assert.IsTrue(conf.Results.Count() == 0);
        }

        [TestMethod]
        public async Task SynchronizeUpdateConflict()
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
                            new StringContent(string.Format(responseContent, send.ToString(), string.Empty, 0))
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

            MobileServiceConflictsResolvedException ex = null;
            try
            {
                JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table"), this.http.Object, jObj, parameters);
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.Conflicts.Count() == 1);

            ResolvedConflict conf = ex.Conflicts.First();

            Assert.AreEqual("lastWriteWins", conf.ResolveStrategy);
            Assert.AreEqual(ConflictType.UpdateUpdate, conf.Type);           
        }

        [TestMethod]
        public async Task SynchronizeUpdateConflictLocal()
        {
            ISynchronizer synchronizer = new TimestampSynchronizer(this.storage.Object, this.conflictResolver.Object);

            #region Setup

            bool firstSent = false;

            this.http.Setup(h => h.SendAsync(It.IsAny<HttpRequestMessage>()))
                .Returns<HttpRequestMessage>(async r =>
                {
                    if(!firstSent)
                    {
                        firstSent = true;
                        return new HttpResponseMessage(HttpStatusCode.Conflict)
                        {
                            Content =
                                new StringContent(string.Format(conflictContent, newItem, currentItem, 0))
                        };
                    }
                    else
                    {
                        if (r.Content != null)
                        {
                            var sendjson = await r.Content.ReadAsStringAsync();
                            JObject send = JObject.Parse(sendjson);
                            send["__version"] = "00000000";
                            return new HttpResponseMessage()
                            {
                                Content =
                                    new StringContent(string.Format(responseContent, send.ToString(), string.Empty, 0))
                            };
                        }
                        else
                        {
                            return new HttpResponseMessage()
                            {
                                Content =
                                    new StringContent(string.Format(responseContent, string.Empty, string.Empty, 0))
                            };
                        }
                    }
                });
            ConflictResult cresult = new ConflictResult();
            cresult.ModifiedItems.Add(currentItem);
            cresult.NewItems.Add(newItem);
            cresult.DeletedItems.Add(deletedItem);
            this.conflictResolver.Setup(cr => cr.Resolve(It.IsAny<Conflict>())).Returns(Task.FromResult(cresult));

            #endregion

            JObject jObj = JObject.Parse(
                        @"{
                            ""id"": ""B1A60844-236A-43EA-851F-7DCD7D5755FA"",
                            ""status"": 2,
                            ""__version"": ""00000000"",
                            ""text"": ""text""
                        }"
                    );

            MobileServiceConflictsResolvedException ex = null;
            try
            {
                JObject result = await synchronizer.UploadChanges(new Uri("http://localhost/tables/table"), this.http.Object, jObj, parameters);
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.Conflicts.Count() == 1);

            ResolvedConflict conf = ex.Conflicts.First();

            Assert.AreEqual("client", conf.ResolveStrategy);
            Assert.AreEqual(ConflictType.UpdateUpdate, conf.Type);
        }
    }
}
