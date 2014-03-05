using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Caching;

namespace Microsoft.WindowsAzure.Mobile.Caching.Test
{
    [TestClass]
    public class MobileServiceClientExtensionsTest
    {
        [TestMethod]
        public void UseOfflineDataCapabilitiesForTablesTest()
        {
            MobileServiceClient client = new MobileServiceClient(new Uri("https://localhost/"), "applicationKey");

            MobileServiceClient newClient = client.UseOfflineDataCapabilitiesForTables(new NoNetworkInformation());

            Assert.AreEqual(client.ApplicationUri, newClient.ApplicationUri);
            Assert.AreEqual(client.ApplicationKey, newClient.ApplicationKey);
        }
    }
}
