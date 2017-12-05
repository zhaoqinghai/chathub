using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using Google.Protobuf.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GrpcProcess
{
    public class GrpcProcessEntry
    {
        #region 进程关闭
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            switch (sig)
            {
                case CtrlType.CTRL_C_EVENT:
                    return true;
                case CtrlType.CTRL_LOGOFF_EVENT:
                    return true;
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                    if (_client.State == ChannelState.Ready)
                    {
                        _client.ShutDown();
                    }
                    return false;
                default:
                    return true;
            }
        }
        #endregion

        static Grpc.GrpcClient _client;
        static void Main(string[] args)
        {
            StartGrpcProcess();
            Config();
            DoWork();
            //CancellationTokenSource cts = new CancellationTokenSource();
            //var t1 = Task.Delay(Timeout.Infinite, cts.Token);
            //var t2 = Task.Delay(Timeout.Infinite);

            //Task.Delay(3000).ContinueWith(t => {
            //    cts.Cancel(); });
            //try
            //{
            //    Task.WhenAny(new Task[] { t1, t2 }).Wait();
            //}
            //catch
            //{
            //    Console.WriteLine("llllladfasdf");
            //}

            //Console.ReadLine();
        }

        static void Config()
        {
            _client.PropertyChanged -= Client_PropertyChanged;
            _client.PropertyChanged += Client_PropertyChanged;
            //_handler -= new EventHandler(Handler);
            //_handler += new EventHandler(Handler);
            //SetConsoleCtrlHandler(_handler, true);
        }

        static void DoWork()
        {
            var command = string.Empty;
            var user = new UserInfo();
            while (true)
            {
                switch (command.ToLower())
                {
                    case "login":

                        user.Status = Status.Online;
                        Console.WriteLine("输入用户名");
                        user.Name = Console.ReadLine();
                        Console.WriteLine("输入id");
                        try
                        {
                            user.Id = ulong.Parse(Console.ReadLine());
                        }
                        catch
                        {
                            Console.WriteLine("输入id为一串数字");
                            break;
                        }
                        _client.Login(user, new Action<ResponseInfo>(obj =>
                        {
                            if (obj is ResponseInfo)
                            {
                                switch (((ResponseInfo)obj).Code)
                                {
                                    case 1:
                                        var userInfo = JsonConvert.DeserializeObject<UserInfo>(((ResponseInfo)obj).JsonData);
                                        Console.WriteLine($"{userInfo.Name}:{userInfo.Status}");
                                        break;
                                    case 2:
                                        var messageInfo = JsonConvert.DeserializeObject<SendMessageInfo>(((ResponseInfo)obj).JsonData);
                                        Console.WriteLine($"{messageInfo.SenderId}:{messageInfo.MessageData}");
                                        break;
                                }
                            }
                        }));
                        break;
                    case "sendmessage":
                        if(user.Id != 0)
                        {
                            Console.WriteLine("请输入发送的消息.....");
                            _client.SendMessage(new SendMessageInfo() { SenderId = user.Id, MessageData = Console.ReadLine(), IsBoard = true }).Wait();
                        }
                        else
                        {
                            Console.WriteLine("请先登录......");
                        }
                        break;
                    case "":
                        break;
                    default:
                        Console.WriteLine("无法识别该命令");
                        break;
                }
                if (command.ToLower() == "quit")
                {
                    Console.WriteLine("即将退出客户端.........");
                    Thread.Sleep(1000);
                    return;
                }
                command = Console.ReadLine();
            }
        }
        static void OnProcessExit(object sender, EventArgs e)
        {
            _client.ShutDown();
        }


        private static async void Client_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if(e.PropertyName == nameof(Grpc.GrpcClient.State))
            {
                if (_client.State != ChannelState.Ready)
                {
                    Console.WriteLine($"{_client.State}.....");
                    await Task.Delay(10000);
                    if(_client.State != ChannelState.Ready)
                    {
                        Console.WriteLine("连接失败!");
                    }
                }
                Console.WriteLine("连接成功!");
            }
        }

        static async void StartGrpcProcess()
        {
            _client = Grpc.GrpcClient.Create(new IPEndPoint(IPAddress.Loopback, 9999));
            Console.WriteLine("启动连接....");
            try
            {
                await _client.StartConnectAsync();
            }
            catch
            {
                Console.WriteLine("连接异常.........");
            }
        }

    }


}
