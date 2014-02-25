using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public enum ConflictType
    {
        UpdateUpdate = 0,

        UpdateDelete = 1,

        DeleteUpdate = 2,

        DeleteDelete = 3,
    }
}
