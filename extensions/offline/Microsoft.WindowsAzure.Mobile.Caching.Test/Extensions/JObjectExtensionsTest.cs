using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test
{
    [TestClass]
    public class JObjectExtensionsTest
    {
        [TestMethod]
        public void RemoveCreatesNewJObjectWithPropertiesRemoved()
        {
            JObject obj = new JObject() { { "prop1", "value1" }, { "prop2", "value2" } };
            JObject obj2 = obj.Remove(prop => prop.Name.Equals("prop1"));

            Assert.AreNotSame(obj, obj2);
            Assert.IsTrue(((IDictionary<string,JToken>)obj2).ContainsKey("prop2"));
            Assert.IsFalse(((IDictionary<string,JToken>)obj2).ContainsKey("prop1"));
        }
    }
}
