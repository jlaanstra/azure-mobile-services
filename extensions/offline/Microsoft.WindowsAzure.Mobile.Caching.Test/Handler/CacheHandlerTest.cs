using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class CacheHandlerTest
    {
        private Mock<ICacheProvider> provider;
        private CacheHandler handler;
        private HttpClient httpClient;
        private HttpContent returnContent = new StringContent(@"{
                    ""results"": [],
                }");

        [TestInitialize]
        public void Setup()
        {
            this.provider = new Mock<ICacheProvider>();
            this.provider.Setup(ic => ic.Read(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.Is<IHttp>(h => h != null))).Returns(Task.FromResult(returnContent));
            this.provider.Setup(ic => ic.Insert(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.IsAny<HttpContent>(), It.Is<IHttp>(h => h != null))).Returns(Task.FromResult(returnContent));
            this.provider.Setup(ic => ic.Update(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.IsAny<HttpContent>(), It.Is<IHttp>(h => h != null))).Returns(Task.FromResult(returnContent));
            this.provider.Setup(ic => ic.Delete(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.Is<IHttp>(h => h != null))).Returns(Task.FromResult(returnContent));
            this.provider.Setup(ic => ic.ProvidesCacheForRequest(It.IsAny<Uri>())).Returns(true);

            this.handler = new CacheHandler(this.provider.Object);
            this.handler.InnerHandler = new TestHandler();
            this.httpClient = new HttpClient(this.handler);
        }

        [TestMethod]
        public async Task CacheHandlerInitializesHttp()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");
                        
            var response = await this.httpClient.SendAsync(request);

            this.provider.Verify(ic => ic.Read(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.Is<IHttp>(h => h != null)), Times.Once);

            Assert.AreEqual(returnContent, response.Content);
            provider.Verify();
        }

        [TestMethod]
        public async Task CacheHandlerCallInsertForPost()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/");

            var response = await this.httpClient.SendAsync(request);

            this.provider.Verify(ic => ic.Insert(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.IsAny<HttpContent>(), It.Is<IHttp>(h => h != null)), Times.Once);

            Assert.AreEqual(returnContent, response.Content);
            provider.Verify();
        }

        [TestMethod]
        public async Task CacheHandlerCallUpdateForPatch()
        {
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), "http://localhost/");

            var response = await this.httpClient.SendAsync(request);

            this.provider.Verify(ic => ic.Update(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.IsAny<HttpContent>(), It.Is<IHttp>(h => h != null)), Times.Once);

            Assert.AreEqual(returnContent, response.Content);
            provider.Verify();
        }

        [TestMethod]
        public async Task CacheHandlerCallDeleteForDelete()
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, "http://localhost/");

            var response = await this.httpClient.SendAsync(request);

            this.provider.Verify(ic => ic.Delete(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")), It.Is<IHttp>(h => h != null)), Times.Once);

            Assert.AreEqual(returnContent, response.Content);
            provider.Verify();
        }

        [TestMethod]
        public async Task CacheHandlerSendRequestIfNoCache()
        {
            this.provider.Setup(ic => ic.ProvidesCacheForRequest(It.IsAny<Uri>())).Returns(false);

            var request = new HttpRequestMessage(HttpMethod.Delete, "http://localhost/");

            var response = await this.httpClient.SendAsync(request);

            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        public class TestHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }
        }
    }
}
