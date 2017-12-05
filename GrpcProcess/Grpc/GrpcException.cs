using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrpcProcess.Grpc
{
    public class GrpcException : Exception
    {
        private GrpcErrorCode _code;
        private GrpcException()
        {

        }
        public GrpcException(GrpcErrorCode code)
        {
            _code = code;
        }

        public override string Message
        {
            get
            {
                string message = string.Empty;
                switch (_code)
                {
                    case GrpcErrorCode.NullChannel:
                        message = "channel为空!";
                        break;
                    case GrpcErrorCode.NullClient:
                        message = "client为空!";
                        break;
                }
                return message;
            }
        }
    }
    
    public enum GrpcErrorCode
    {
        NullChannel = 0x01,
        NullClient = 0x02,
    }
}
