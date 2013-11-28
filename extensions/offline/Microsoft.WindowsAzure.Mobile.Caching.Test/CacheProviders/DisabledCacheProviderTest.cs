using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching.CacheProviders;
using Moq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class DisabledCacheProviderTest
    {
        private Uri testUri = new Uri("http://localhost/");
        private StringContent testContent = new StringContent("Test", Encoding.UTF8);
        private Mock<IHttp> http;

        [TestInitialize]
        public void Setup()
        {
            this.http = new Mock<IHttp>();
        }

        [TestMethod]
        public void DisabledCacheProviderDoesNotCacheForUri()
        {
            var disabled = new DisabledCacheProvider();

            Assert.IsFalse(disabled.ProvidesCacheForRequest(testUri));
        }

        [TestMethod]
        public async Task ReadShouldPassTheUriOn()
        {
            var disabled = new DisabledCacheProvider();

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = testContent;
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            disabled.Http = this.http.Object;

            HttpContent content = await disabled.Read(testUri);

            Assert.AreEqual(testUri, request.RequestUri);
            Assert.AreEqual(testContent, content);
            Assert.AreEqual(HttpMethod.Get, request.Method);
        }

        [TestMethod]
        public async Task InsertShouldPassTheUriAndContentOn()
        {
            var disabled = new DisabledCacheProvider();

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Post;
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = testContent;
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            disabled.Http = this.http.Object;

            HttpContent result = await disabled.Insert(testUri, testContent);

            Assert.AreEqual(testUri, request.RequestUri);
            Assert.AreEqual(testContent, request.Content);
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual(testContent, result);
        }

        [TestMethod]
        public async Task UpdateShouldPassTheUriAndContentOn()
        {
            var disabled = new DisabledCacheProvider();

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = new HttpMethod("PATCH");
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = testContent;
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            disabled.Http = this.http.Object;

            HttpContent result = await disabled.Update(testUri, testContent);

            Assert.AreEqual(testUri, request.RequestUri);
            Assert.AreEqual(testContent, request.Content);
            Assert.AreEqual(new HttpMethod("PATCH"), request.Method);
            Assert.AreEqual(testContent, result);
        }

        [TestMethod]
        public async Task DeleteShouldPassTheUriOn()
        {
            var disabled = new DisabledCacheProvider();

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Delete;
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = null;
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);

            disabled.Http = this.http.Object;

            HttpContent result = await disabled.Delete(testUri);

            Assert.AreEqual(testUri, request.RequestUri);
            Assert.AreEqual(null, request.Content);
            Assert.AreEqual(HttpMethod.Delete, request.Method);
            Assert.AreEqual(null, result);
        }
    }
}
