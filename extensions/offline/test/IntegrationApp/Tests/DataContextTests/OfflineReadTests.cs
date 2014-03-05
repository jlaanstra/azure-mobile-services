using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrationApp.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Test;
using Microsoft.WindowsAzure.MobileServices.TestFramework;

namespace IntegrationApp.Tests.DataContextTests
{
    public class OfflineReadTests : OfflineTestBase
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
                    Price = i % 2 == 0 ? 30.09M : 9.09M,
                    Type = i % 2 == 0 ? ProductType.Food : ProductType.Furniture,
                    Weight = i % 2 == 0 ? 35.7f : (float?)null,
                });
            }
        }

        [AsyncTestMethod]
        public async Task OfflineQueryUsesCachedData()
        {
            await this.CacheProvider.Purge();

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.Where(p => p.AvailableTime > TimeSpan.FromHours(3)).ToEnumerableAsync();

            Assert.AreEqual(6, results.Count());

            this.NetworkInformation.IsOnline = false;

            results = await table.Where(p => p.OtherId < 7).ToEnumerableAsync();
            Assert.AreEqual(3, results.Count());

            this.NetworkInformation.IsOnline = true;
        }

        [Tag("test")]
        [AsyncTestMethod]
        public async Task OfflineQueryUsesCachedData2()
        {
            await this.CacheProvider.Purge();

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ToEnumerableAsync();

            Assert.AreEqual(10, results.Count());

            this.NetworkInformation.IsOnline = false;

            results = await table.Where(p => p.OtherId < 9 && p.Price < p.Price * 2 - 10 && p.Name.EndsWith("6")).ToEnumerableAsync();
            Assert.AreEqual(1, results.Count());

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task OfflineQueryUsesCachedData3()
        {
            await this.CacheProvider.Purge();

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.Where(p => p.InStock).ToEnumerableAsync();

            Assert.AreEqual(5, results.Count());

            this.NetworkInformation.IsOnline = false;

            results = await table.Where(p => p.Weight == null).ToEnumerableAsync();
            Assert.AreEqual(0, results.Count());

            this.NetworkInformation.IsOnline = true;
        }
    }
}
