using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    class HttpStatusCodeException : Exception
    {
        public HttpStatusCodeException(HttpStatusCode code, string message)
            : base(message)
        {
            this.StatusCode = code;
        }

        public HttpStatusCode StatusCode { get; private set; }
    }
}
