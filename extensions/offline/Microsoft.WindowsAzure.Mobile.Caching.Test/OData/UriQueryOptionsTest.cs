using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test.OData
{
    [TestClass]
    public class UriQueryOptionsTest
    {
        [TestMethod]
        public void UriQueryOptionsConstructorTest()
        {
            IQueryOptions query = new UriQueryOptions(
                new Uri("https://localhost/tables/table/?$filter=Price eq 34&$orderby=Price&$top=5&$skip=5&$inlinecount=allpages"));

            Assert.AreEqual("Price eq 34", query.Filter.RawValue);
            Assert.AreEqual("Price", query.OrderBy.RawValue);
            Assert.AreEqual("5", query.Top.RawValue);
            Assert.AreEqual("5", query.Skip.RawValue);
            Assert.AreEqual("allpages", query.InlineCount.RawValue);
        }

        [TestMethod]
        public void UriQueryOptionsConstructorThrowsTest()
        {
            Exception e = null;
            try
            {
                IQueryOptions query = new UriQueryOptions(
                new Uri("https://localhost/tables/table/?$filter="));
            }
            catch(Exception ex)
            {
                e = ex;
            }

            Assert.IsNotNull(e);
        }
    }
}
