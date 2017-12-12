using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using static PushServer.PushService;
using Newtonsoft.Json;
using  System.Collections.Generic;
using System.Collections.Concurrent;
/***************************************
code=1获取单独用户状态
code=2获取消息信息
code=3获取当前在线人数
****************************************/

namespace PushServer
{
    public class PushSeverEntry
    {
        const int port = 9999;
        static Server server;
        static void Main(string[] args)
        {
            server = new Server
            {
                Services = { PushService.BindService(new GrpcServies()) },
                Ports = { new ServerPort("localhost",port,ServerCredentials.Insecure)}
            };
        
            server.Start();
            MessageQueue.Start();
            Console.ReadLine();
        }
        public static async void ShutDown()
        {
            await server.ShutdownAsync();
        }
    }
    public class GrpcServies : PushServiceBase
    {
        public override Task Login(UserInfo request, IServerStreamWriter<ResponseInfo> responseStream, ServerCallContext context) => Task.Factory.StartNew(() =>
        {
            var callClient = new CallClient(){Id = request.Id,UserInfo = request,StreamWriter = new WeakReference<IServerStreamWriter<ResponseInfo>>(responseStream,false)};
            try
            {
                ClientManager.AddCallClient(callClient);
                var list = ClientManager.GetCurrentAllKeys();
                var clientQueue = new Queue<UserInfo>();
                foreach(var item in list){
                    if(item != request.Id){
                        var callClientTemp = ClientManager.GetCallClient(item);
                        if(callClientTemp!=null){
                            CallClient callClientTempTarget;
                            if(callClientTemp.TryGetTarget(out callClientTempTarget)){
                                clientQueue.Enqueue(callClientTempTarget.UserInfo);
                                callClientTempTarget.SendData(new ResponseInfo(){
                                        Code = 1,
                                        JsonData = JsonConvert.SerializeObject(request)
                                    });
                            }
                        }
                    }
                }
                var responseInfo =  new ResponseInfo(){
                        Code = 3,
                        JsonData = JsonConvert.SerializeObject(clientQueue)
                    };
                callClient.SendData(responseInfo);
                Task.Delay(Timeout.Infinite,context.CancellationToken).Wait();
            }
            catch
            {
                Console.WriteLine("i love bug");
            }
            finally{
                callClient.StreamWriter = null;
                request.Status = Status.Offline;
                ClientManager.DelCallClient(request.Id);
                var list = ClientManager.GetCurrentAllKeys();
                foreach(var item in list){
                    if(item != request.Id){
                        var callClientTemp = ClientManager.GetCallClient(item);
                        if(callClientTemp!=null){
                            CallClient callClientTempTarget;
                            if(callClientTemp.TryGetTarget(out callClientTempTarget)){
                                callClientTempTarget.SendData(new ResponseInfo(){
                                        Code = 1,
                                        JsonData = JsonConvert.SerializeObject(request)
                                    });
                            }
                        }
                    }
                }
            }
        });
        
      
        public override Task<ResponseInfo> SendMessage(SendMessageInfo request, ServerCallContext context) => Task<ResponseInfo>.Factory.StartNew(()=>{
            if(request.IsBoard){
                var keys = ClientManager.GetCurrentAllKeys();
                foreach(var key in keys){
                    if(key!=request.SenderId){
                        if(ClientManager.GetCallClient(key)!=null){
                            CallClient callClient;
                            if(ClientManager.GetCallClient(key).TryGetTarget(out callClient)){
                                var responseInfo = new ResponseInfo(){
                                    Code = 2,
                                    JsonData = JsonConvert.SerializeObject(request)
                                    };
                                callClient.SendData(responseInfo);
                            }
                        }
                    }
                }
            }
            else{
                foreach(var item in request.ReceiverId){
                    CallClient callClient;
                    if(ClientManager.GetCallClient(item).TryGetTarget(out callClient)){
                        var responseInfo = new ResponseInfo(){
                            Code = 2,
                            JsonData = JsonConvert.SerializeObject(request)
                            };
                        callClient.SendData(responseInfo);
                    } 
                }
            }
            return new ResponseInfo();
        });
    }
    
    
}
