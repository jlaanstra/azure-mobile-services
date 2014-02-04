using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class HttpTest
    {
        private IHttp http;
        private HttpResponseMessage response;

        [TestInitialize]
        public void Setup()
        {
            this.http = new Http(req => Task.FromResult(response));
            this.http.OriginalRequest = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
            this.http.OriginalRequest.Headers.Add("X-ZUMO", "TEST");
        }

        [TestMethod]
        public void CreateRequestTest()
        {
            HttpRequestMessage req = this.http.CreateRequest(HttpMethod.Post, new Uri("http://testurl/"), new Dictionary<string, string>() { { "X-TEST", "TEST" } });

            Assert.AreEqual(HttpMethod.Post, req.Method);
            Assert.AreEqual(new Uri("http://testurl/"), req.RequestUri);
            Assert.AreEqual(2, req.Headers.Count());
            Assert.IsTrue(req.Headers.Contains("X-ZUMO"));
            Assert.IsTrue(req.Headers.Contains("X-TEST"));
            Assert.AreEqual(this.http.OriginalRequest.Version, req.Version);
        }

        [TestMethod]
        public async Task SendOriginalAsyncTest()
        {
            this.response = new HttpResponseMessage();

            HttpResponseMessage resp = await this.http.SendOriginalAsync();

            Assert.IsNotNull(resp);
            Assert.AreSame(this.response, resp);
            Assert.IsNotNull(this.http.OriginalResponse);
            Assert.AreSame(this.response, this.http.OriginalResponse);
        }

        [TestMethod]
        public async Task GetJsonAsyncTest()
        {
            this.response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                @"{
                    ""results"": [],
                    ""deleted"": [],
                    ""__version"": ""00000001""
                }") };

            JObject obj = await this.http.GetJsonAsync(this.http.OriginalRequest);

            Assert.IsNotNull(obj);
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("results"));
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("deleted"));            
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("__version"));
        }

        [TestMethod]
        public async Task GetJsonAsyncThrowsOnConflictTest()
        {
            this.response = new HttpResponseMessage(HttpStatusCode.Conflict) { Content = new StringContent(
                @"{
                    ""currentItem"": { 
                        ""id"": ""DFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"",
                        ""__version"": ""00000002"",
                    },
                    ""newItem"": { 
                        ""id"": ""EFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"",
                        ""__version"": ""00000001"",
                    },
                    ""__version"": ""00000002""
                }") };

            HttpStatusCodeException e = null;
            JObject conflict = null;
            try
            {                
                JObject obj = await this.http.GetJsonAsync(this.http.OriginalRequest);
            }
            catch(HttpStatusCodeException ex)
            {
                e = ex;
                conflict = JObject.Parse(ex.Message);
            }

            Assert.IsNotNull(e);
            Assert.IsNotNull(conflict);
            Assert.AreEqual(HttpStatusCode.Conflict, e.StatusCode);
            Assert.IsTrue(((IDictionary<string, JToken>)conflict).ContainsKey("currentItem"));
            Assert.IsTrue(((IDictionary<string, JToken>)conflict).ContainsKey("newItem"));            
            Assert.IsTrue(((IDictionary<string, JToken>)conflict).ContainsKey("__version"));
        }
    }
}
