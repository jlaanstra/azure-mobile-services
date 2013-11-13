using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class MemoryCacheProviderTest
    {
        private Uri testUri = new Uri("http://localhost/");
        private StringContent testContent = new StringContent("Test", Encoding.UTF8);

        [TestMethod]
        public void IsByDefaultChachingEverything()
        {
            var memory = new MemoryCacheProvider(TimeSpan.FromMinutes(1));

            Assert.IsTrue(memory.ProvidesCacheForRequest(new Uri("http://localhost/")));
        }

        [TestMethod]
        public async Task ReadShouldCacheForTheTimeSpanProvided()
        {
            var memory = new MemoryCacheProvider(TimeSpan.FromSeconds(5));
            HttpContent empty = new StringContent(string.Empty);

            HttpContent resultContent = testContent;

            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = (u, c, m) =>
            {
                return Task.FromResult(resultContent);
            };

            HttpContent result = await memory.Read(testUri, getResponse);
            Assert.AreEqual(testContent, result);

            resultContent = empty;

            result = await memory.Read(testUri, getResponse);
            Assert.AreEqual(testContent, result);

            await Task.Delay(5000);

            result = await memory.Read(testUri, getResponse);
            Assert.AreEqual(empty, result);
        }
    }
}
