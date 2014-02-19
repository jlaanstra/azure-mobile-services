using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class MemoryCacheProviderTest
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
        public void IsByDefaultCachingEverything()
        {
            var memory = new MemoryCacheProvider(TimeSpan.FromMinutes(1));

            Assert.IsTrue(memory.ProvidesCacheForRequest(new Uri("http://localhost/")));
        }

        [TestMethod]
        public async Task ReadShouldCacheForTheTimeSpanProvided()
        {
            #region Setup
            var memory = new MemoryCacheProvider(TimeSpan.FromSeconds(5));
            HttpContent empty = new StringContent(string.Empty);

            HttpRequestMessage request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = testContent;
            this.http.Setup(h => h.SendOriginalAsync()).Returns(() => Task.FromResult(response));
            this.http.SetupGet(h => h.OriginalRequest).Returns(request);
            #endregion

            HttpContent result = await memory.Read(testUri, this.http.Object);
            Assert.AreEqual(testContent, result);

            response.Content = empty;

            result = await memory.Read(testUri, this.http.Object);
            Assert.AreEqual(testContent, result);

            await Task.Delay(5000);

            result = await memory.Read(testUri, this.http.Object);
            Assert.AreEqual(empty, result);
        }
    }
}
