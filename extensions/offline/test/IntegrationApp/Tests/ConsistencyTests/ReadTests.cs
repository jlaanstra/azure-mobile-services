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
    public class ReadTests : OfflineTestBase
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

            for(int i = 0; i < 10; i++)
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
        public async Task BasicReadTable()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());
        }

        [AsyncTestMethod]
        public async Task ReadingTableTwiceReturnsSameResponse()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());

            results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());
        }

        [AsyncTestMethod]
        public async Task ReadingTableWhenOffline()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task BasicQueryTable()
        {
            IMobileServiceTableQuery<Product> table = this.OfflineClient.GetTable<Product>().Where(p => p.DisplayAisle == 19);
            IEnumerable<Product> results = await table.ToEnumerableAsync();

            Assert.AreEqual(1, results.Count());

            table = this.OfflineClient.GetTable<Product>().Where(p => p.DisplayAisle == 9);
            results = await table.ToEnumerableAsync();

            Assert.AreEqual(0, results.Count());
        }

        [AsyncTestMethod]
        public async Task QueryingTableTwiceReturnsSameResponse()
        {
            IMobileServiceTableQuery<Product> table = this.OfflineClient.GetTable<Product>().Where(p => p.DisplayAisle == 19);
            IEnumerable<Product> results = await table.ToEnumerableAsync();

            Assert.AreEqual(1, results.Count());

            table = this.OfflineClient.GetTable<Product>().Where(p => p.DisplayAisle == 19);
            results = await table.ToEnumerableAsync();

            Assert.AreEqual(1, results.Count());
        }

        [AsyncTestMethod]
        public async Task QueryingTableWhenOffline()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTableQuery<Product> table = this.OfflineClient.GetTable<Product>().Where(p => p.AvailableTime > TimeSpan.FromHours(5));
            IEnumerable<Product> results = await table.ToEnumerableAsync();

            Assert.AreEqual(4, results.Count());

            table = this.OfflineClient.GetTable<Product>().Where(p => p.Type == ProductType.Furniture);
            results = await table.ToEnumerableAsync();

            Assert.AreEqual(5, results.Count());

            this.NetworkInformation.IsOnline = true;
        }
    }
}
