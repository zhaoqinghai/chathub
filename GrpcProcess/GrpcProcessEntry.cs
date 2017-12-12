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

        static UserInfo _currentUser = new UserInfo();

        static IList<UserInfo> _onlineUserList = new List<UserInfo>();
        static void Main(string[] args)
        {
            StartGrpcProcess();
            Config();
            DoWork();
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
            string tip = string.Empty;

            while (true)
            {
                if (_currentUser.Id != 0)
                {
                    Console.WriteLine("用户已登录,请输入命令...");
                }
                command = Console.ReadLine();
                switch (command.ToLower())
                {
                    case "login":
                        _currentUser.Status = Status.Online;
                        Console.WriteLine("输入用户名...");
                        _currentUser.Name = Console.ReadLine();
                        Console.WriteLine("输入电话号码...");
                        try
                        {
                            _currentUser.Id = ulong.Parse(Console.ReadLine());
                        }
                        catch
                        {
                            Console.WriteLine("输入id为一串数字");
                            break;
                        }
                        _client.Login(_currentUser, new Action<ResponseInfo>(obj =>
                        {
                            if (obj is ResponseInfo)
                            {
                                switch (((ResponseInfo)obj).Code)
                                {
                                    case 1:
                                        var userInfo = JsonConvert.DeserializeObject<UserInfo>(((ResponseInfo)obj).JsonData);
                                        var list = _onlineUserList.Where(item => item.Id == userInfo.Id);
                                        if (userInfo.Status == Status.Online)
                                        {
                                            if(!list.Any())
                                                _onlineUserList.Add(userInfo);
                                        }
                                        else
                                        {
                                           
                                            if (list.Any())
                                            {
                                                _onlineUserList.Remove(list.FirstOrDefault());
                                            }
                                        }
                                        Console.WriteLine($"{userInfo.Name}:{userInfo.Status}");
                                        break;
                                    case 2:
                                        var messageInfo = JsonConvert.DeserializeObject<SendMessageInfo>(((ResponseInfo)obj).JsonData);
                                        var senderUserInfo = _onlineUserList.FirstOrDefault(item => item.Id == messageInfo.SenderId);
                                        Console.WriteLine($"{senderUserInfo.Name}:{messageInfo.MessageData}");
                                        break;
                                    case 3:
                                        var userQueue = JsonConvert.DeserializeObject<Queue<UserInfo>>(((ResponseInfo)obj).JsonData);
                                        while(userQueue.Count > 0)
                                        {
                                            _onlineUserList.Add(userQueue.Dequeue());
                                        }
                                        break;
                                }
                            }
                        }));
                        break;
                    case "boardmessage":
                        if(_currentUser.Id != 0 && _client.State == ChannelState.Ready)
                        {
                            Console.WriteLine("请输入广播的消息.....");
                            _client.SendMessage(new SendMessageInfo() { SenderId = _currentUser.Id, MessageData = Console.ReadLine(), IsBoard = true });
                        }
                        else
                        {
                            Console.WriteLine("请先登录......");
                        }
                        break;
                    case "sendmessage":
                        if (_currentUser.Id != 0 && _client.State == ChannelState.Ready)
                        {
                            Console.WriteLine("请输入用户名（名字间用空格隔开）....");

                            var usesString = Console.ReadLine();
                            var userInfoArray = usesString.Split(' ');
                            var recieverIdList = _onlineUserList.Where(item => userInfoArray.Contains(item.Name)).Select(item=> item.Id);
                            Console.WriteLine("请输入发送的消息.....");
                            var sendMessage = Console.ReadLine();
                            var sendMessageInfo = new SendMessageInfo() { SenderId = _currentUser.Id, MessageData = sendMessage, IsBoard = false };
                            foreach (var item in recieverIdList)
                            {
                                sendMessageInfo.ReceiverId.Add(item);
                            }
                            _client.SendMessage(sendMessageInfo);
                        }
                        else
                        {
                            Console.WriteLine("请先登录......");
                        }
                        
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
                    Console.WriteLine("启动连接....");
                    try
                    {
                        await _client.StartConnectAsync();
                    }
                    catch
                    {
                        Console.WriteLine("连接异常.........");
                        await Task.Delay(1000).ContinueWith(t => { Process.GetCurrentProcess().Kill(); });
                    }
                }
                _currentUser = new UserInfo();
                _onlineUserList.Clear();
                Console.WriteLine("连接成功!");
                Console.WriteLine("请登录...");
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
                await Task.Delay(1000).ContinueWith(t => { Process.GetCurrentProcess().Kill(); });
            }
        }

    }


}
