using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public interface ISynchronizer
    {
        void NotifyOfUnsynchronizedChange();

        Task Synchronize(string tableName);
    }
}
