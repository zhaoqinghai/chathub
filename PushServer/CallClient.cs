using System;
using Grpc.Core;
namespace PushServer{
    public class CallClient{
        public void SendData(ResponseInfo data){
            try{
                IServerStreamWriter<ResponseInfo> streamWriter;
                if(StreamWriter.TryGetTarget(out streamWriter)){
                    MessageQueue.EnterMessageQueue(data,streamWriter);
                }
                else{
                    throw new InvalidOperationException();
                }
            }
            catch{
                ClientManager.DelCallClient(this.Id);
            }
        }

        public ulong Id { get; set; }

        public UserInfo UserInfo{get;set;}

        public WeakReference<IServerStreamWriter<ResponseInfo>> StreamWriter{get;set;}

    }
}