using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class Conflict
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Conflict"/> class.
        /// </summary>
        /// <param name="version">The version.</param>
        /// <param name="type">The type.</param>
        /// <param name="currentItem">The current item.</param>
        /// <param name="newItem">The new item.</param>
        public Conflict(string __version, ConflictType conflictType, JObject currentItem, JObject newItem)
        {
            this.Version = __version;
            this.Type = conflictType;
            this.CurrentItem = currentItem;
            this.NewItem = newItem;
        }

        public string Version { get; private set; }

        public ConflictType Type { get; private set; }

        public JObject CurrentItem { get; private set; }

        public JObject NewItem { get; private set; }
    }
}
