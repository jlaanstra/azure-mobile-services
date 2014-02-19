using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrationApp.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Test;
using Microsoft.WindowsAzure.MobileServices.TestFramework;

namespace IntegrationApp.Tests.ResponseTimeTests
{
    public class ReadResponseTimesNormalTests : OfflineTestBase
    {
        public override async Task Initialize()
        {
            await base.Initialize();

            await this.PrepareTableAsync(this.NormalClient);
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

            for (int i = 0; i < 50; i++)
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

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest01()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest02()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest03()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest04()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest05()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest06()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest07()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest08()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest09()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task ReadAllItemsNormalTest10()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(50, results.Count());
        }
    }
}
