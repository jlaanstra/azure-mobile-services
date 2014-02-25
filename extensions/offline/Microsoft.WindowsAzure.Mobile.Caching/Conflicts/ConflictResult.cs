using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class ConflictResult
    {
        public ConflictResult()
        {
            this.NewItems = new List<JObject>();
            this.ModifiedItems = new List<JObject>();
            this.DeletedItems = new List<JObject>();
        }

        public IList<JObject> NewItems { get; private set; }

        public IList<JObject> ModifiedItems { get; private set; }

        public IList<JObject> DeletedItems { get; private set; }
    }
}
