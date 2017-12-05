using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using static GrpcProcess.Grpc.PushService;
using System.Threading;
using System.ComponentModel;

namespace GrpcProcess.Grpc
{

    public class GrpcClient : PushServiceClient , INotifyPropertyChanged
    {
        private static Channel _grpcChannel;

        private static GrpcClient _client;

        private ThreadLocal<CancellationTokenSource> _localCts = new ThreadLocal<CancellationTokenSource>();

        public event PropertyChangedEventHandler PropertyChanged;

        public event Func<ResponseInfo> RecieveDataEvent;

        private ChannelState _state;
        public ChannelState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
                if (_client != null)
                    PropertyChanged.Invoke(_client, new PropertyChangedEventArgs(nameof(State)));
            }
        }

        private GrpcClient()
        {
            
        }

        protected GrpcClient(Channel channel) : base(channel)
        {

        }

        public static GrpcClient Create(IPEndPoint port)
        {
            if (_client != null)
            {
                return _client;
            }
            _grpcChannel = new Channel($"{port.Address}:{port.Port}", ChannelCredentials.Insecure);
            _client = new GrpcClient(_grpcChannel);
            return _client;
        }

        public async Task StartConnectAsync()
        {
            if (_grpcChannel != null)
            {
                await _grpcChannel.ConnectAsync();
                if (_grpcChannel.State != ChannelState.Ready)
                {
                    while(await Task.Delay(1000).ContinueWith<bool>(t => true))
                    {
                        try
                        {
                            await _grpcChannel.WaitForStateChangedAsync(_grpcChannel.State, deadline: DateTime.UtcNow.AddSeconds(4)).ContinueWith(t =>
                            {
                                State = _grpcChannel.State;
                            });
                        }
                        catch
                        {
                            Console.WriteLine("正在连接中.....");
                        }
                        if (_grpcChannel.State == ChannelState.Ready)
                        {
                            State = ChannelState.Ready;
                            return;
                        }
                    }
                    
                }
                State = ChannelState.Ready;
                return;

            }
            throw (new GrpcException(GrpcErrorCode.NullChannel));
        }

        public async Task RecieveDataLoop(AsyncServerStreamingCall<ResponseInfo> stream, Action<ResponseInfo> action)
        {
            _localCts.Value = new CancellationTokenSource(Timeout.Infinite);
            while (await stream.ResponseStream.MoveNext(_localCts.Value.Token))
            {
                try
                {
                    var response = stream.ResponseStream.Current;
                    action.Invoke(response);
                }
                catch(Exception ex)
                {
                    HandleException(ex);
                }
                finally
                {
                    _localCts.Value = new CancellationTokenSource(Timeout.Infinite);
                }
            }
        }

        public async Task Login(UserInfo userInfo,Action<ResponseInfo> action)
        {
            var stream = _client.Login(userInfo);
            try
            {
                await RecieveDataLoop(stream, action);
            }
            catch(Exception ex)
            {
                HandleException(ex);
            }
        }


        public async Task<ResponseInfo> SendMessage(SendMessageInfo messageInfo,CancellationTokenSource cts = null)
        {
            if (_client != null)
            {
                if (!_localCts.IsValueCreated)
                {
                    _localCts.Value = cts ?? new CancellationTokenSource(Timeout.Infinite);
                }
                try
                {
                    var response = await _client.SendMessageAsync(messageInfo, deadline: DateTime.UtcNow.AddSeconds(10), cancellationToken: _localCts.Value.Token);
                    return response;
                }
                catch(Exception ex)
                {
                    HandleException(ex);
                }
                return null;
                
            }
            throw (new GrpcException(GrpcErrorCode.NullClient));
        }

        void HandleException(Exception ex)
        {
            if(ex is RpcException)
            {
                if(_grpcChannel.State != ChannelState.Ready)
                {
                    State = _grpcChannel.State;
                }
            }
        }

        public void ShutDown()
        {
            foreach (var item in _localCts?.Values)
            {
                item.Cancel();
            }
            _grpcChannel.ShutdownAsync().Wait();
        }
    }

    
}
