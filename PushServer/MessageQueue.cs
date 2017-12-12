using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Grpc.Core;
using System;

namespace PushServer{
    public class MessageQueue{
        private static ConcurrentQueue<Tuple<ResponseInfo,IServerStreamWriter<ResponseInfo>,Guid,CancellationTokenSource>> _queue = new ConcurrentQueue<Tuple<ResponseInfo,IServerStreamWriter<ResponseInfo>,Guid,CancellationTokenSource>>();

        private static ConcurrentDictionary<Guid,bool> resultsDictionary = new ConcurrentDictionary<Guid, bool>();

        static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        static bool _isSleep = false;

        static bool _isShutDown = false;
        public static Task<bool> EnterMessageQueue(ResponseInfo response,IServerStreamWriter<ResponseInfo> streamWriter){
            if(streamWriter==null){
                throw new InvalidCastException();
            }
            return Task.Factory.StartNew(()=>{
                var cancellationTokenSource = new CancellationTokenSource();
                var guid = Guid.NewGuid();
                resultsDictionary.TryAdd(guid,false);
                bool isSuccess;
                _queue.Enqueue(new Tuple<ResponseInfo,IServerStreamWriter<ResponseInfo>,Guid,CancellationTokenSource>(response,streamWriter,guid,new CancellationTokenSource(TimeSpan.FromMinutes(5))));
                if(_isSleep){
                    _cancellationTokenSource.Cancel();
                }
                try{
                    Task.Delay(Timeout.Infinite,cancellationTokenSource.Token).Wait();
                }
                finally{
                    //这里可以优化
                    resultsDictionary.TryRemove(guid,out isSuccess);
                }
                
                return isSuccess;
            });
        }
        public static void ShutDown(){
            _isShutDown = true;
            if(_isSleep){
                _cancellationTokenSource.Cancel();
            }
        }
        public static async void Start(){
            _isShutDown = false;
            await Task.Yield();
            while(!_isShutDown){
                Tuple<ResponseInfo,IServerStreamWriter<ResponseInfo>,Guid,CancellationTokenSource> response;
                if(_queue.TryDequeue(out response)){
                    bool isSuccess = false;
                    try{
                        await response.Item2.WriteAsync(response.Item1);
                        isSuccess = true;
                    }
                    catch{
                        isSuccess = false;
                    }
                    finally{
                        resultsDictionary.TryUpdate(response.Item3,isSuccess,isSuccess);
                        response.Item4.Cancel();
                    }
                }
                else{
                    if(_queue.Count == 0){
                        _cancellationTokenSource = new CancellationTokenSource();
                        _isSleep = true;
                        try{
                            await Task.Delay(Timeout.Infinite, _cancellationTokenSource.Token);
                        }
                        catch{

                        }
                    }
                }
            }
        }
    }
}