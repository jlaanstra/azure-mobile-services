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
    public class ReadYourWritesTests : OfflineTestBase
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
            Product[] items = results.ToArray();

            foreach (Product item in items)
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
        public async Task ReadShouldReadInserts()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());

            Guid guid = Guid.NewGuid();
            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
                Id = guid.ToString(),
                DisplayAisle = (short)(2 + 10),
                InStock = true,
                Name = "AwesomeProduct" + 2,
                OptionFlags = (byte)5,
                OtherId = 34,
                Price = 30.09M,
                Type = ProductType.Furniture,
                Weight = 35.7f,
            };

            await table.InsertAsync(product);

            results = await table.ReadAsync();

            Assert.AreEqual(11, results.Count());

            await table.DeleteAsync(product);

            results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());

            this.NetworkInformation.IsOnline = true;

        }

        [AsyncTestMethod]
        public async Task QueryShouldReadInserts()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.Where(p => p.Type == ProductType.Furniture).ToEnumerableAsync();

            Assert.AreEqual(5, results.Count());

            Guid guid = Guid.NewGuid();
            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
                Id = guid.ToString(),
                DisplayAisle = (short)(2 + 10),
                InStock = true,
                Name = "AwesomeProduct" + 2,
                OptionFlags = (byte)5,
                OtherId = 34,
                Price = 30.09M,
                Type = ProductType.Furniture,
                Weight = 35.7f,
            };

            await table.InsertAsync(product);

            results = await table.Where(p => p.Type == ProductType.Furniture).ToEnumerableAsync();

            Assert.AreEqual(6, results.Count());

            await table.DeleteAsync(product);

            results = await table.Where(p => p.Type == ProductType.Furniture).ToEnumerableAsync();

            Assert.AreEqual(5, results.Count());

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task ReadShouldReadUpdates()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.ReadAsync();

            Assert.AreEqual(10, results.Count());

            Product product = results.FirstOrDefault(p => p.OtherId == 5);
            product.Weight = 100.45f;
            await table.UpdateAsync(product);

            results = await table.ReadAsync();

            Assert.AreEqual(1, results.Where(p => p.Weight == 100.45f).Count());

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task QueryShouldReadUpdates()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();
            IEnumerable<Product> results = await table.Where(p => p.Type == ProductType.Furniture).ToEnumerableAsync();

            Assert.AreEqual(5, results.Count());

            Product product = results.FirstOrDefault(p => p.OtherId == 5);
            product.Weight = 100.45f;
            await table.UpdateAsync(product);

            results = await table.Where(p => p.Type == ProductType.Furniture).ToEnumerableAsync();

            Assert.AreEqual(1, results.Where(p => p.Weight == 100.45f).Count());

            this.NetworkInformation.IsOnline = true;
        }
    }
}
