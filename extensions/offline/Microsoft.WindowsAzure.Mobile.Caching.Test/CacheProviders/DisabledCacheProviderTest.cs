using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching.CacheProviders;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class DisabledCacheProviderTest
    {
        private Uri testUri = new Uri("http://localhost/");
        private StringContent testContent = new StringContent("Test", Encoding.UTF8);

        [TestMethod]
        public void DisabledCacheProviderDoesNotCacheForUri()
        {
            var disabled = new DisabledCacheProvider();

            Assert.IsFalse(disabled.ProvidesCacheForRequest(new Uri("http://localhost/")));
        }

        [TestMethod]
        public void ReadShouldPassTheRequestOn()
        {
            var disabled = new DisabledCacheProvider();

            Uri uri = null;
            HttpContent content = null;
            HttpMethod method = null;
            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = (u, c, m) =>
            {
                uri = u;
                content = c;
                method = m;

                return Task.FromResult(content);
            };

            disabled.Read(testUri, getResponse);

            Assert.AreEqual(testUri, uri);
            Assert.AreEqual(null, content);
            Assert.AreEqual(HttpMethod.Get, method);
        }

        [TestMethod]
        public void InsertShouldPassTheRequestOn()
        {
            var disabled = new DisabledCacheProvider();

            Uri uri = null;
            HttpContent content = null;
            HttpMethod method = null;
            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = (u, c, m) =>
            {
                uri = u;
                content = c;
                method = m;

                return Task.FromResult(content);
            };

            disabled.Insert(testUri, testContent, getResponse);

            Assert.AreEqual(testUri, uri);
            Assert.AreEqual(testContent, content);
            Assert.AreEqual(HttpMethod.Post, method);
        }

        [TestMethod]
        public void UpdateShouldPassTheRequestOn()
        {
            var disabled = new DisabledCacheProvider();

            Uri uri = null;
            HttpContent content = null;
            HttpMethod method = null;
            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = (u, c, m) =>
            {
                uri = u;
                content = c;
                method = m;

                return Task.FromResult(content);
            };

            disabled.Update(testUri, testContent, getResponse);

            Assert.AreEqual(testUri, uri);
            Assert.AreEqual(testContent, content);
            Assert.AreEqual(new HttpMethod("PATCH"), method);
        }

        [TestMethod]
        public void DeleteShouldPassTheRequestOn()
        {
            var disabled = new DisabledCacheProvider();

            Uri uri = null;
            HttpContent content = null;
            HttpMethod method = null;
            Func<Uri, HttpContent, HttpMethod, Task<HttpContent>> getResponse = (u, c, m) =>
            {
                uri = u;
                content = c;
                method = m;

                return Task.FromResult(content);
            };

            disabled.Delete(testUri, getResponse);

            Assert.AreEqual(testUri, uri);
            Assert.AreEqual(null, content);
            Assert.AreEqual(HttpMethod.Delete, method);
        }
    }
}
