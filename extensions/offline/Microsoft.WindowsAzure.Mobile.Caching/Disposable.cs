using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    internal class Disposable : IDisposable
    {
        private readonly Action dispose;
        public Disposable(Action dispose)
        {
            this.dispose = dispose;
        }
        public void Dispose()
        {
            dispose();
        }
    }
}
