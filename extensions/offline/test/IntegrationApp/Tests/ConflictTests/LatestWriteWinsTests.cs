using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IntegrationApp.Models;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Caching;
using Microsoft.WindowsAzure.MobileServices.Test;
using Microsoft.WindowsAzure.MobileServices.TestFramework;
using Newtonsoft.Json.Linq;

namespace IntegrationApp.Tests.ConflictTests
{
    public class LatestWriteWinsTests : OfflineTestBase
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

        [AsyncTestMethod]
        public async Task UpdateUpdateLatestWriteWins()
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

            await table.UpdateAsync(product);

            string newVersion = product.Version;
            product.Version = oldVersion;
            product.Price = 45.67M;

            IEnumerable<ResolvedConflict> resolved = null; 
            try
            {
                await table.UpdateAsync(product);
            }
            catch(MobileServiceConflictsResolvedException e)
            {
                resolved = e.Conflicts;
            }

            Assert.IsNotNull(resolved);
            Assert.AreEqual(1, resolved.Count());
            Assert.AreEqual("latestWriteWins", resolved.First().ResolveStrategy);
            Assert.AreEqual(ConflictType.UpdateUpdate, resolved.First().Type);
            Assert.AreEqual(1, resolved.First().Results.Count());
            Assert.AreEqual(45.67M, resolved.First().Results.First().Value<decimal>("Price"));
        }

        [AsyncTestMethod]
        public async Task UpdateDeleteLatestWriteWins()
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

            await table.UpdateAsync(product);

            string newVersion = product.Version;
            product.Version = oldVersion;

            // NOTE: this is kind of hacky, but is needed because the way delete are handled.
            using (this.Storage.Open())
            {
                await this.Storage.UpdateData("products", new JArray(new JObject() { { "id", guid }, { "__version", oldVersion } }));
            }

            IEnumerable<ResolvedConflict> resolved = null;
            try
            {
                await table.DeleteAsync(product);
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                resolved = e.Conflicts;
            }

            Assert.IsNotNull(resolved);
            Assert.AreEqual(1, resolved.Count());
            Assert.AreEqual("latestWriteWins", resolved.First().ResolveStrategy);
            Assert.AreEqual(ConflictType.UpdateDelete, resolved.First().Type);
            Assert.AreEqual(0, resolved.First().Results.Count());

            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.IsNull(product2);
        }

        [AsyncTestMethod]
        public async Task DeleteUpdateLatestWriteWins()
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

            await table.DeleteAsync(product);

            //put id back
            product.Id = guid.ToString();
            product.Version = oldVersion;
            product.Price = 45.67M;

            IEnumerable<ResolvedConflict> resolved = null;
            try
            {
                await table.UpdateAsync(product);
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                resolved = e.Conflicts;
            }

            Assert.IsNotNull(resolved);
            Assert.AreEqual(1, resolved.Count());
            Assert.AreEqual("latestWriteWins", resolved.First().ResolveStrategy);
            Assert.AreEqual(ConflictType.DeleteUpdate, resolved.First().Type);
            Assert.AreEqual(1, resolved.First().Results.Count());

            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.AreEqual(45.67M, product2.Price);
        }

        [AsyncTestMethod]
        public async Task DeleteDeleteLatestWriteWins()
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

            //we have to add the item back to storage directly to simulate this conflict
            JArray jObj = null;
            using (this.Storage.Open())
            {
                jObj = await this.Storage.GetStoredData("products", new StaticQueryOptions() { Filter = new FilterQuery(string.Format("id eq '{0}'", Uri.EscapeDataString(guid.ToString().Replace("'", "''")))) });
            }
            await table.DeleteAsync(product);
            using(this.Storage.Open())
            {
                await this.Storage.StoreData("products", jObj, false);
            }

            //put id back
            product.Id = guid.ToString();
            product.Version = oldVersion;

            IEnumerable<ResolvedConflict> resolved = null;
            try
            {
                await table.DeleteAsync(product);
            }
            catch (MobileServiceConflictsResolvedException e)
            {
                resolved = e.Conflicts;
            }

            Assert.IsNotNull(resolved);
            Assert.AreEqual(1, resolved.Count());
            Assert.AreEqual("latestWriteWins", resolved.First().ResolveStrategy);
            Assert.AreEqual(ConflictType.DeleteDelete, resolved.First().Type);
            Assert.AreEqual(0, resolved.First().Results.Count());

            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.IsNull(product2);
        }
    }
}
