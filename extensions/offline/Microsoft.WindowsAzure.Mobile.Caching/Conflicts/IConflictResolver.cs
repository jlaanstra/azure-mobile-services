using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface IConflictResolver
    {
        Task<ConflictResult> Resolve(Conflict conflict);
    }
}
