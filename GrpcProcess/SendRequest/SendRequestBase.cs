using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrpcProcess.SendRequest
{
    public abstract class SendRequestBase
    {
        public abstract string SendRequest(SendRequestParameter parameter);
    }
}
