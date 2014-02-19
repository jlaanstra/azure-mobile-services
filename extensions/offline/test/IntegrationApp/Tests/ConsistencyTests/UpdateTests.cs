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
    public class UpdateTests : OfflineTestBase
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
        public async Task BasicUpdate()
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

            await table.UpdateAsync(product);
            //temp until Lookup works
            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.AreEqual(20.79M, product.Price);
            Assert.AreEqual(20.79M, product2.Price);
        }

        [AsyncTestMethod]
        public async Task BasicUpdateOffline()
        {
            this.NetworkInformation.IsOnline = false;

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

            await table.UpdateAsync(product);
            Product product2 = (await table.Where(p => p.Id == guid.ToString()).ToEnumerableAsync()).FirstOrDefault();

            Assert.AreEqual(20.79M, product.Price);
            Assert.AreEqual(20.79M, product2.Price);

            this.NetworkInformation.IsOnline = true;
        }        
    }
}
