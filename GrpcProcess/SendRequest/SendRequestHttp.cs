using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

namespace GrpcProcess.SendRequest
{
    public class SendRequestHttp : SendRequestBase
    {
        public override string SendRequest(SendRequestParameter parameter)
        {
            HttpWebRequest request = HttpWebRequest.Create(parameter.Url) as HttpWebRequest;
            return string.Empty;
        }
    }
}
