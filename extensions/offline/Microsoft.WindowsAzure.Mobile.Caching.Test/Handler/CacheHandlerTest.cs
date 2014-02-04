using System;
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
            this.provider.Setup(ic => ic.Read(It.Is<Uri>(u => u.OriginalString.Equals("http://localhost/")))).Returns(Task.FromResult(returnContent));
            this.provider.Setup(ic => ic.ProvidesCacheForRequest(It.IsAny<Uri>())).Returns(true);

            this.handler = new CacheHandler(this.provider.Object);
            this.httpClient = new HttpClient(this.handler);
        }

        [TestMethod]
        public async Task CacheHandlerInitializesHttp()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/");

            this.provider.VerifySet(ic => ic.Http = It.IsAny<IHttp>(), Times.Once);

            var response = await this.httpClient.SendAsync(request);

            Assert.AreEqual(returnContent, response.Content);
            provider.VerifyAll();
        }
    }
}
