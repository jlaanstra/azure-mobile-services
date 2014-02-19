using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrationApp.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Test;
using Microsoft.WindowsAzure.MobileServices.TestFramework;
using Newtonsoft.Json.Linq;

namespace IntegrationApp.Tests.ConflictTests
{
    public class ServerWinsTest : OfflineTestBase
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
        }

        [Tag("test")]
        [AsyncTestMethod]
        public async Task UpdateUpdateServerWins()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

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

            product.Price = 20.79M;
            // store version to simulate conflict
            string oldVersion = product.Version;

            await table.UpdateAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            string newVersion = product.Version;
            product.Version = oldVersion;
            product.Price = 45.67M;

            await table.UpdateAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            Assert.AreEqual(20.79M, product.Price);
        }

        [Tag("temp")]
        [Tag("test")]
        [AsyncTestMethod]
        public async Task UpdateDeleteServerWins()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

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

            product.Price = 20.79M;
            // store version to simulate conflict
            string oldVersion = product.Version;

            await table.UpdateAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            string newVersion = product.Version;
            product.Version = oldVersion;

            // NOTE: this is kind of hacky, but is needed because the way delete are handled.
            using (this.Storage.Open())
            {
                await this.Storage.UpdateData("products", new JArray(new JObject() { { "id", guid }, { "__version", oldVersion } }));
            }

            await table.DeleteAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });
            
            //reading will cause the conflicted delete to sync.
            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.IsNotNull(product2);
            Assert.AreEqual(20.79M, product.Price);
            Assert.AreNotEqual(newVersion, product2.Version);
            Assert.AreNotEqual(oldVersion, product2.Version);
        }

        [Tag("test")]
        [AsyncTestMethod]
        public async Task DeleteUpdateServerWins()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

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

            product.Price = 20.79M;
            // store version to simulate conflict
            string oldVersion = product.Version;

            await table.DeleteAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            //put id back
            product.Id = guid.ToString();
            product.Version = oldVersion;
            product.Price = 45.67M;

            await table.UpdateAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.IsNull(product2);
        }

        [Tag("test")]
        [AsyncTestMethod]
        public async Task DeleteDeleteServerWins()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

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

            product.Price = 20.79M;
            // store version to simulate conflict
            string oldVersion = product.Version;

            await table.DeleteAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            //put id back
            product.Id = guid.ToString();
            product.Version = oldVersion;

            await table.DeleteAsync(product, new Dictionary<string, string>() { { "resolveStrategy", "serverWins" } });

            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.IsNull(product2);
        }
    }
}
