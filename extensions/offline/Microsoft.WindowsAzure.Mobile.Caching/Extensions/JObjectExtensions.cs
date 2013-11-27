using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public static class JObjectExtensions
    {
        public static JObject Remove(this JObject This, Func<JProperty, bool> filter)
        {
            var jobj = new JObject();
            foreach (var prop in This.Properties().Where(filter))
            {
                jobj.Add(prop.Name, prop.Value);
            }
            return jobj;
        }
    }
}
