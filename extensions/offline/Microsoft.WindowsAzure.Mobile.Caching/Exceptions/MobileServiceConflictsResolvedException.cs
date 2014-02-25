using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class MobileServiceConflictsResolvedException : Exception
    {
        public MobileServiceConflictsResolvedException(IList<ResolvedConflict> conflicts)
            : base("One or more conflicts where resolved while synchronizing.")
        {
            this.Conflicts = conflicts;
        }

        public IEnumerable<ResolvedConflict> Conflicts { get; private set; }
    }
}
