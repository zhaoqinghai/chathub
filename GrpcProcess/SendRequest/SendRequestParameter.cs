using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrpcProcess.SendRequest
{
    public class SendRequestParameter
    {
        /// <summary>
        /// 请求参数
        /// </summary>
        public Dictionary<string,object> SendParameter { get; set; }
        /// <summary>
        /// 请求的网址
        /// </summary>
        public string Url { get; set; }
    }
}
