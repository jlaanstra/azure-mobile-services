using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    public delegate void TypedEventHandler<TSender, TEventArgs>(TSender sender, TEventArgs e);
}
