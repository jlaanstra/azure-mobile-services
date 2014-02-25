using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class NoConflictResolver : IConflictResolver
    {
        public async Task<ConflictResult> Resolve(Conflict conflict)
        {
            return new ConflictResult();
        }
    }
}
