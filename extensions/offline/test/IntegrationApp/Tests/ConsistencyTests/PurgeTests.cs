using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrationApp.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Test;
using Microsoft.WindowsAzure.MobileServices.TestFramework;

namespace IntegrationApp.Tests.ConsistencyTests
{
    public class PurgeTests : OfflineTestBase
    {
        public override async Task Initialize()
        {
            await base.Initialize();

            await this.PrepareTableAsync(this.OfflineClient);
        }

        private async Task PrepareTableAsync(MobileServiceClient client)
        {
            // Make sure the table is empty
            IMobileServiceTable<Product> table = client.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            foreach (Product item in results)
            {
                await table.DeleteAsync(item);
            }

            for (int i = 0; i < 10; i++)
            {
                await table.InsertAsync(new Product()
                {
                    AvailableTime = TimeSpan.FromHours(i),
                    Id = Guid.NewGuid().ToString(),
                    DisplayAisle = (short)(i + 10),
                    InStock = i % 2 == 0,
                    Name = "Product" + i,
                    OptionFlags = (byte)i,
                    OtherId = i,
                    Price = 30.09M,
                    Type = i % 2 == 0 ? ProductType.Food : ProductType.Furniture,
                    Weight = i % 2 == 0 ? 35.7f : (float?)null,
                });
            }
        }

        [AsyncTestMethod]
        public async Task PurgeTestOnlineHasNoEffect()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();
            Assert.AreEqual(10, results.Count());

            await this.CacheProvider.Purge();

            results = await table.ReadAsync();
            Assert.AreEqual(10, results.Count());
        }

        [AsyncTestMethod]
        public async Task PurgeTestOfflineHasEffect()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();
            Assert.AreEqual(10, results.Count());

            this.NetworkInformation.IsOnline = false;

            await this.CacheProvider.Purge();

            results = await table.ReadAsync();
            Assert.AreEqual(0, results.Count());

            this.NetworkInformation.IsOnline = true;
        }
    }
}
