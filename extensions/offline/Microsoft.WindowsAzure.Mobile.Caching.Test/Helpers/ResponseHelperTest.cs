using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    /// <summary>
    /// Summary description for ResponseHelperTest
    /// </summary>
    [TestClass]
    public class ResponseHelperTest
    {
        [TestMethod]
        public async Task GetResponseAsJObjectTest()
        {
            HttpContent content = new StringContent(@"{
                    ""results"": [],
                    ""deleted"": [],
                    ""__version"": ""00000001""
                }");

            JObject obj = await ResponseHelper.GetResponseAsJObject(content);

            Assert.IsNotNull(obj);
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("results"));
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("deleted"));
            Assert.IsTrue(((IDictionary<string, JToken>)obj).ContainsKey("__version"));
        }

        [TestMethod]
        public async Task GetResultsJArrayFromJsonTest()
        {
            HttpContent content = new StringContent(@"{
                    ""results"": [{
                        ""id"": ""DFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"",
                        ""__version"": ""00000002"",
                    }],
                    ""deleted"": [],
                    ""__version"": ""00000001""
                }");

            JObject obj = await ResponseHelper.GetResponseAsJObject(content);

            JArray array = ResponseHelper.GetResultsJArrayFromJson(obj);

            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count);
            Assert.AreEqual("DFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF", array.First["id"].ToString());
        }

        [TestMethod]
        public async Task GetDeletedJArrayFromJsonTest()
        {
            HttpContent content = new StringContent(@"{
                    ""results"": [{
                        ""id"": ""DFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"",
                        ""__version"": ""00000002"",
                    }],
                    ""deleted"": [ ""EFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"" ],
                    ""__version"": ""00000001""
                }");

            JObject obj = await ResponseHelper.GetResponseAsJObject(content);

            IEnumerable<string> array = ResponseHelper.GetDeletedJArrayFromJson(obj);

            Assert.IsNotNull(array);
            Assert.AreEqual(1, array.Count());
            Assert.AreEqual("EFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF", array.First().ToString());
        }

        [TestMethod]
        public async Task EnsureValidSyncResponseThrowsTest()
        {
            JObject obj = JObject.Parse(@"{
                    ""results"": [{
                        ""id"": ""DFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"",
                        ""__version"": ""00000002"",
                    }],
                    ""deleted"": [ ""EFF4AF4F-FA27-44A2-9F3B-6B52C47D27AF"" ],
                    ""__stamp"": ""00000001""
                }");

            Exception ex = null;
            try
            {
                ResponseHelper.EnsureValidSyncResponse(obj);
            }
            catch(InvalidOperationException e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
        }
    }
}
