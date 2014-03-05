using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.WindowsAzure.MobileServices.Caching.Test.Helpers
{
    [TestClass]
    public class UriHelperTest
    {
        [TestMethod]
        public void GetIdFromUriTest()
        {
            string id = UriHelper.GetIdFromUri(new Uri("http://localhost/tables/table/34"));

            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.AreEqual("34", id);
        }

        [TestMethod]
        public void GetIdFromUriWithTrailingSlashTest()
        {
            string id = UriHelper.GetIdFromUri(new Uri("http://localhost/tables/table/34/"));

            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.AreEqual("34", id);
        }

        [TestMethod]
        public void GetIdFromUriGuidTest()
        {
            string id = UriHelper.GetIdFromUri(new Uri("http://localhost/tables/table/98397639-DF26-41B2-A0BC-B79056B193A6"));

            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.AreEqual("98397639-DF26-41B2-A0BC-B79056B193A6", id);
        }
        [TestMethod]
        public void GetIdFromUriWithQueryTest()
        {
            string id = UriHelper.GetIdFromUri(new Uri("http://localhost/tables/table/98397639-DF26-41B2-A0BC-B79056B193A6?test=true"));

            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.AreEqual("98397639-DF26-41B2-A0BC-B79056B193A6", id);
        }

        [TestMethod]
        public void GetTableNameFromUriTest()
        {
            string tableName = UriHelper.GetTableNameFromUri(new Uri("http://localhost/tables/test"));

            Assert.IsFalse(string.IsNullOrEmpty(tableName));
            Assert.AreEqual("test", tableName);
        }

        [TestMethod]
        public void GetTableNameFromUriTrailingSlashTest()
        {
            string tableName = UriHelper.GetTableNameFromUri(new Uri("http://localhost/tables/test/"));

            Assert.IsFalse(string.IsNullOrEmpty(tableName));
            Assert.AreEqual("test", tableName);
        }

        [TestMethod]
        public void GetTableNameFromUriWithContentTest()
        {
            string tableName = UriHelper.GetTableNameFromUri(new Uri("http://localhost/tables/test/some/content/testing/"));

            Assert.IsFalse(string.IsNullOrEmpty(tableName));
            Assert.AreEqual("test", tableName);
        }

        [TestMethod]
        public void GetTableNameFromUriWithQueryTest()
        {
            string tableName = UriHelper.GetTableNameFromUri(new Uri("http://localhost/tables/test/some/?test=true"));

            Assert.IsFalse(string.IsNullOrEmpty(tableName));
            Assert.AreEqual("test", tableName);
        }

        [TestMethod]
        public void GetCleanTableUriTest()
        {
            Uri tableUri = UriHelper.GetCleanTableUri(new Uri("http://localhost/tables/test/"));

            Assert.IsNotNull(tableUri);
            Assert.AreEqual("http://localhost/tables/test", tableUri.OriginalString);
        }

        [TestMethod]
        public void GetCleanTableUriWithContentTest()
        {
            Uri tableUri = UriHelper.GetCleanTableUri(new Uri("http://localhost/tables/test/some/content/"));

            Assert.IsNotNull(tableUri);
            Assert.AreEqual("http://localhost/tables/test", tableUri.OriginalString);
        }

        [TestMethod]
        public void GetCleanTableUriWithQueryTest()
        {
            Uri tableUri = UriHelper.GetCleanTableUri(new Uri("http://localhost/tables/test/some/sontent?test=true"));

            Assert.IsNotNull(tableUri);
            Assert.AreEqual("http://localhost/tables/test", tableUri.OriginalString);
        }
    }
}
