using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public class LatestWriteWinsResolver : IConflictResolver
    {
        public Task<ConflictResult> Resolve(Conflict conflict)
        {
            conflict.NewItem["__version"] = conflict.Version;
            ConflictResult result = new ConflictResult();
            switch(conflict.Type)
            {
                case ConflictType.DeleteDelete:
                case ConflictType.UpdateDelete:
                    result.DeletedItems.Add(conflict.NewItem);
                    break;
                case ConflictType.DeleteUpdate:
                case ConflictType.UpdateUpdate:
                    result.ModifiedItems.Add(conflict.NewItem);
                    break;
            }
            return Task.FromResult(result);
        }
    }
}
