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
            
            Console.ReadLine();
        }
        public static async void ShutDown()
        {
            await server.ShutdownAsync();
        }
    }

    public class PushData{
        private static ConcurrentDictionary<ulong,ClientInfo> ClientList = new ConcurrentDictionary<ulong,ClientInfo>();

        public static ConcurrentDictionary<ulong,ClientInfo> GetClientList(){
            return ClientList;
        }

        public static void AddClient(ClientInfo clientInfo){
            ICollection<ulong> keyCollection;
            //线程安全不用加锁
            ClientList.TryAdd(clientInfo.Id,clientInfo);
            keyCollection = ClientList.Keys;

            Task.Factory.StartNew(()=>{
                ClientInfo client;
                if(ClientList.TryGetValue(clientInfo.Id,out client)){
                    var clientQueue = new Queue<UserInfo>();
                    foreach(var key in keyCollection){
                        if(key!=clientInfo.Id){
                            
                            ClientInfo otherClient;
                            if(ClientList.TryGetValue(key,out otherClient)){
                                clientQueue.Enqueue(otherClient.UserInfo);
                                otherClient.AddMessage( new ResponseInfo(){
                                        Code = 1,
                                        JsonData = JsonConvert.SerializeObject(client.UserInfo)
                                    });
                                
                            }
                        }
                    }
                    var responseInfo =  new ResponseInfo(){
                        Code = 3,
                        JsonData = JsonConvert.SerializeObject(clientQueue)
                    };
                    client.AddMessage(responseInfo); 
                }
            });
        }

        public static void DelClient(ulong id){
            ICollection<ulong> keyCollection;
            ClientInfo clientInfo;
            //线程安全不用加锁
            ClientList.TryRemove(id,out clientInfo);
            keyCollection = ClientList.Keys;
            
            Task.Factory.StartNew(()=>{
                if(clientInfo!=null){
                    foreach(var key in keyCollection){
                        ClientInfo otherClient;
                        if(ClientList.TryGetValue(key,out otherClient)){
                            clientInfo.UserInfo.Status = Status.Offline;
                            otherClient.AddMessage(new ResponseInfo(){
                                    Code = 1,
                                    JsonData = JsonConvert.SerializeObject(clientInfo.UserInfo)
                                }
                            );
                            
                        }
                    }
                }
                
                
            });
        }

        
    }

    public class ClientInfo : IDisposable
    {
        CancellationTokenSource cancellationTokenSource;
        public ulong Id{
            get{
                return UserInfo.Id;
            }
        }
        public UserInfo UserInfo{
            get;set;
        }

        private ConcurrentQueue<ResponseInfo> MessageQueue = new ConcurrentQueue<ResponseInfo>();

        public void AddMessage(ResponseInfo responseInfo){
            MessageQueue.Enqueue(responseInfo);
            if(cancellationTokenSource!=null){
                if(!cancellationTokenSource.IsCancellationRequested){
                    cancellationTokenSource.Cancel();
                }
            }
        }

        public ResponseInfo CurrentResponseInfo;

        public bool MessageQueueMoveNext(CancellationToken token){
            ResponseInfo responseInfo;
            if(MessageQueue.TryDequeue(out responseInfo)){
                CurrentResponseInfo = responseInfo;
            }
            else{
                cancellationTokenSource = new CancellationTokenSource();
                try{
                    var t1 = Task.Delay(Timeout.Infinite,cancellationTokenSource.Token);
                    var t2 = Task.Delay(Timeout.Infinite,token);
                    Task.WhenAny(new Task[]{t1,t2}).Wait();
                    if(token.IsCancellationRequested){
                        return false;
                    }
                }
                finally{
                    MessageQueue.TryDequeue(out responseInfo);
                    CurrentResponseInfo = responseInfo;
                }
            }
            return true;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
    public class GrpcServies : PushServiceBase
    {
        public override Task Login(UserInfo request, IServerStreamWriter<ResponseInfo> responseStream, ServerCallContext context) => Task.Factory.StartNew(() =>
         {
             ClientInfo client = null;
             try
             {
                 client = new ClientInfo() { UserInfo = request };
                 PushData.AddClient(client);
                 while (client.MessageQueueMoveNext(context.CancellationToken))
                 {
                     if(client.CurrentResponseInfo!=null){
                         responseStream.WriteAsync(client.CurrentResponseInfo).Wait();
                     }
                 }
             }
             catch
             {
                 Console.WriteLine("i love bug");
             }
             finally{
                 PushData.DelClient(request.Id);
                 client?.Dispose();
             }
         });
        
      
        public override Task<ResponseInfo> SendMessage(SendMessageInfo request, ServerCallContext context) => Task<ResponseInfo>.Factory.StartNew(()=>{
            if(request.IsBoard){
                var clientList = PushData.GetClientList();
                var keys = clientList.Keys;
                foreach(var key in keys){
                    if(key!=request.SenderId){
                        ClientInfo otherClient;
                        if(clientList.TryGetValue(key,out otherClient)){
                            var responseInfo = new ResponseInfo(){
                                Code = 2,
                                JsonData = JsonConvert.SerializeObject(request)
                            };
                            otherClient.AddMessage(responseInfo);
                        }
                    }
                }
            }
            else{
                foreach(var item in request.ReceiverId){
                    var clientList = PushData.GetClientList();
                    ClientInfo otherClient;
                    if(clientList.TryGetValue(item,out otherClient)){
                        var responseInfo = new ResponseInfo(){
                            Code = 2,
                            JsonData = JsonConvert.SerializeObject(request)
                        };
                        otherClient.AddMessage(responseInfo);
                    }
                }
            }
            
            return new ResponseInfo();
        });
    }
    
    
}
