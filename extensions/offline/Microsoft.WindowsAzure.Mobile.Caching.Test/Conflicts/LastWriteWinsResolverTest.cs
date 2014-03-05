using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test.Conflicts
{
    [TestClass]
    public class LastWriteWinsResolverTest
    {
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


        [TestMethod]
        public async Task ResolveUpdateUpdateTest()
        {
            LastWriteWinsResolver resolver = new LastWriteWinsResolver();

            ConflictResult res = await resolver.Resolve(new Conflict("01234567", ConflictType.UpdateUpdate, currentItem, newItem));

            Assert.IsNotNull(res);
            Assert.AreEqual(1, res.ModifiedItems.Count);
            Assert.AreEqual(0, res.NewItems.Count);
            Assert.AreEqual(0, res.DeletedItems.Count);

            Assert.AreEqual("newtext", res.ModifiedItems[0]["text"].ToString());
            Assert.AreEqual("01234567", res.ModifiedItems[0]["__version"].ToString());
        }

        [TestMethod]
        public async Task ResolveUpdateDeleteTest()
        {
            LastWriteWinsResolver resolver = new LastWriteWinsResolver();

            ConflictResult res = await resolver.Resolve(new Conflict("01234567", ConflictType.UpdateDelete, currentItem, newItem));

            Assert.IsNotNull(res);
            Assert.AreEqual(0, res.ModifiedItems.Count);
            Assert.AreEqual(0, res.NewItems.Count);
            Assert.AreEqual(1, res.DeletedItems.Count);

            Assert.AreEqual("newtext", res.DeletedItems[0]["text"].ToString());
            Assert.AreEqual("01234567", res.DeletedItems[0]["__version"].ToString());
        }
    }
}
