using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;

namespace IntegrationApp.Models
{
    [DataTable("products")]
    public class Product
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonIgnore]
        public Guid Guid
        {
            get { return new Guid(Id); }
            set { Id = value.ToString(); }
        }

        [CreatedAt]
        [JsonProperty("__createdAt")]
        public string CreatedAt { get; set; }

        [UpdatedAt]
        [JsonProperty("__updatedAt")]
        public string UpdatedAt { get; set; }

        [Version]
        [JsonProperty("__version")]
        public string Version { get; set; }

        public int OtherId { get; set; }
        public string Name { get; set; }

        public float? Weight { get; set; }
        
        [JsonProperty(Required = Required.Always)]
        public Decimal Price { get; set; }
        public bool InStock { get; set; }
        public short DisplayAisle { get; set; }
        public byte OptionFlags { get; set; }
        public TimeSpan AvailableTime { get; set; }
        public ProductType Type { get; set; }

        public Product()
        {
        }
    }

    public enum ProductType
    {
        Food,
        Furniture,
    }
}
