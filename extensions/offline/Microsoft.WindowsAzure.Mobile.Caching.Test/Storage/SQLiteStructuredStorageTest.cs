using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class SQLiteStructuredStorageTest
    {
        private SQLiteStructuredStorage storage;

        [TestInitialize]
        public void Setup()
        {
            storage = new SQLiteStructuredStorage("test");
        }

        [TestMethod]
        public void GetColumnsFromItemsTest()
        {
            JArray array = new JArray();
            array.Add(new JObject()
            {
                { "integer", 1 },
                { "value1", "string"},
                { "boolean", true }
            });

            IDictionary<string, Column> cols = storage.GetColumnsFromItems(array);

            Assert.AreEqual(3, cols.Count);

            Assert.IsTrue(cols.ContainsKey("integer"));
            // json.net makes int long
            Assert.AreEqual(typeof(long), cols["integer"].GetClrDataType());
            Assert.IsTrue(cols.ContainsKey("value1"));
            Assert.AreEqual(typeof(string), cols["value1"].GetClrDataType());
            Assert.IsTrue(cols.ContainsKey("boolean"));
            Assert.AreEqual(typeof(bool), cols["boolean"].GetClrDataType());
        }

        [TestMethod]
        public void GetColumnsFromItemsFallthroughNullTest()
        {
            JArray array = new JArray();
            array.Add(new JObject()
            {
                { "integer", 1 },
                { "value1", "string"},
                { "boolean", true },
                { "null", null }

            });
            array.Add(new JObject()
            {
                { "integer", 1 },
                { "value1", "string"},
                { "boolean", true },
                { "null", DateTime.Now }
            });

            IDictionary<string, Column> cols = storage.GetColumnsFromItems(array);

            Assert.AreEqual(4, cols.Count);

            Assert.IsTrue(cols.ContainsKey("integer"));
            // json.net makes int long
            Assert.AreEqual(typeof(long), cols["integer"].GetClrDataType());
            Assert.IsTrue(cols.ContainsKey("value1"));
            Assert.AreEqual(typeof(string), cols["value1"].GetClrDataType());
            Assert.IsTrue(cols.ContainsKey("boolean"));
            Assert.AreEqual(typeof(bool), cols["boolean"].GetClrDataType());
            Assert.IsTrue(cols.ContainsKey("null"));
            Assert.AreEqual(typeof(DateTime), cols["null"].GetClrDataType());
        }
    }
}
