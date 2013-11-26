using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;

namespace Todo
{
    public class TodoItem2
    {
        [JsonProperty("id")]
        public Guid Id { get; set; }

        [CreatedAt]
        [JsonProperty("__createdAt")]
        public string CreatedAt { get; set; }

        [UpdatedAt]
        [JsonProperty("__updatedAt")]
        public string UpdatedAt { get; set; }

        [Version]
        [JsonProperty("__version")]
        public string Version { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("complete")]
        public bool Complete { get; set; }
    }
}
