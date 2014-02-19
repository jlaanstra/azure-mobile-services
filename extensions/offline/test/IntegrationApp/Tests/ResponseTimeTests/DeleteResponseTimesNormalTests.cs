﻿using System;
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
    public class DeleteResponseTimesNormalTests : OfflineTestBase
    {
        private Product[] products;

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

            products = new Product[50];

            for (int i = 0; i < 50; i++)
            {
                string id = Guid.NewGuid().ToString();
                Product p = new Product()
                {
                    AvailableTime = TimeSpan.FromHours(i),
                    Id = id,
                    DisplayAisle = (short)(i + 10),
                    InStock = i % 2 == 0,
                    Name = "Product" + i,
                    OptionFlags = (byte)i,
                    OtherId = i,
                    Price = 30.09M,
                    Type = i % 2 == 0 ? ProductType.Food : ProductType.Furniture,
                    Weight = i % 2 == 0 ? 35.7f : (float?)null,
                };

                products[i] = p;

                await table.InsertAsync(p);
            }
        }

        [Tag("ResponseTime")]
        [AsyncTestMethod]
        public async Task Delete50ProductsNormalTest()
        {
            IMobileServiceTable<Product> table = this.NormalClient.GetTable<Product>();

            for (int i = 0; i < 50; i++)
            {
                Product p = products[i];
                await table.DeleteAsync(p);
            }
        }
    }
}
