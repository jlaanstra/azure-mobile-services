using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ResolvedConflict
    {
        internal ResolvedConflict(string resolveStrategy, ConflictType type, IEnumerable<JObject> results)
        {
            this.ResolveStrategy = resolveStrategy;
            this.Type = type;
            this.Results = results;
        }

        public string ResolveStrategy { get; private set; }

        public ConflictType Type { get; private set; }

        public IEnumerable<JObject> Results { get; private set; }
    }
}
