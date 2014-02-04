using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices
{
    public class NetworkInformation : INetworkInformation
    {
        public Task<bool> IsConnectedToInternet()
        {
            return Task.Run(() =>
            {
                int Desc;
                return InternetGetConnectedState(out Desc, 0);
            });
        }

        //Creating the extern function...
        [DllImport("wininet.dll")]
        private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);
    }
}
