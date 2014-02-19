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
    public class InsertTests : OfflineTestBase
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
        public async Task BasicInsertWithId()
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

            Assert.IsNotNull(product.CreatedAt);
            Assert.IsNotNull(product.UpdatedAt);
            Assert.AreEqual(guid.ToString(), product.Id);
            Assert.AreEqual(TimeSpan.FromHours(2), product.AvailableTime);
            Assert.AreEqual((short)12, product.DisplayAisle);
            Assert.AreEqual(true, product.InStock);
            Assert.AreEqual("AwesomeProduct2", product.Name);
            Assert.AreEqual((byte)5, product.OptionFlags);
            Assert.AreEqual(34, product.OtherId);
            Assert.AreEqual(30.09M, product.Price);
            Assert.AreEqual(ProductType.Furniture, product.Type);
            Assert.AreEqual(35.7f, product.Weight);
        }

        [AsyncTestMethod]
        public async Task BasicInsertWithoutId()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
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

            Assert.IsNotNull(product.Id);
            Assert.IsNotNull(product.CreatedAt);
            Assert.IsNotNull(product.UpdatedAt);
            Assert.AreEqual(TimeSpan.FromHours(2), product.AvailableTime);
            Assert.AreEqual((short)12, product.DisplayAisle);
            Assert.AreEqual(true, product.InStock);
            Assert.AreEqual("AwesomeProduct2", product.Name);
            Assert.AreEqual((byte)5, product.OptionFlags);
            Assert.AreEqual(34, product.OtherId);
            Assert.AreEqual(30.09M, product.Price);
            Assert.AreEqual(ProductType.Furniture, product.Type);
            Assert.AreEqual(35.7f, product.Weight);
        }

        [AsyncTestMethod]
        public async Task BasicOfflineInsertWithId()
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

            Assert.IsNull(product.CreatedAt);
            Assert.IsNull(product.UpdatedAt);
            Assert.AreEqual(guid.ToString(), product.Id);
            Assert.AreEqual(TimeSpan.FromHours(2), product.AvailableTime);
            Assert.AreEqual((short)12, product.DisplayAisle);
            Assert.AreEqual(true, product.InStock);
            Assert.AreEqual("AwesomeProduct2", product.Name);
            Assert.AreEqual((byte)5, product.OptionFlags);
            Assert.AreEqual(34, product.OtherId);
            Assert.AreEqual(30.09M, product.Price);
            Assert.AreEqual(ProductType.Furniture, product.Type);
            Assert.AreEqual(35.7f, product.Weight);

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task BasicOfflineInsertWithoutId()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
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

            Assert.IsNotNull(product.Id);
            Assert.IsNull(product.CreatedAt);
            Assert.IsNull(product.UpdatedAt);
            Assert.AreEqual(TimeSpan.FromHours(2), product.AvailableTime);
            Assert.AreEqual((short)12, product.DisplayAisle);
            Assert.AreEqual(true, product.InStock);
            Assert.AreEqual("AwesomeProduct2", product.Name);
            Assert.AreEqual((byte)5, product.OptionFlags);
            Assert.AreEqual(34, product.OtherId);
            Assert.AreEqual(30.09M, product.Price);
            Assert.AreEqual(ProductType.Furniture, product.Type);
            Assert.AreEqual(35.7f, product.Weight);

            this.NetworkInformation.IsOnline = true;
        }

        [AsyncTestMethod]
        public async Task InsertWithSameIdShouldThrow()
        {
            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

            Guid guid = Guid.NewGuid();
            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
                Id = guid.ToString(),
                Version = "AAAAAAAACIQ=",
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

            Exception ex = null;
            try
            {
                await table.InsertAsync(product);
            }
            catch(Exception e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);
        }

        [AsyncTestMethod]
        public async Task InsertWithSameIdShouldThrowOffline()
        {
            this.NetworkInformation.IsOnline = false;

            IMobileServiceTable<Product> table = this.OfflineClient.GetTable<Product>();

            Guid guid = Guid.NewGuid();
            Product product = new Product()
            {
                AvailableTime = TimeSpan.FromHours(2),
                Id = guid.ToString(),
                Version = "AAAAAAAACIQ=",
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

            Exception ex = null;
            try
            {
                await table.InsertAsync(product);
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.IsNotNull(ex);

            this.NetworkInformation.IsOnline = true;
        }
    }
}
