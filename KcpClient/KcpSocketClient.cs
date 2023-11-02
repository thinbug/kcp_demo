using NetLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

//说明

///
/*
 * 握手过程
 * 如果客户端连接，需要发送，{0(空数据),KcpFlag.ConnectRequest(连接类型),ConnectKey(连接密钥)}
 * 服务端根据连接类型，判断需要连接，返回给客户端conv编号,返回格式为{0(空),KcpFlag.AllowConnectConv(连接类型)}
 * 客户端收到conv编号，创建kcp，并连接服务端，发送udp数据{0,KcpFlag.ConnectKcpRequest,自己的conv编号}
 * 服务端再次收到后，验证没问题就发送kcp连接成功。前面的都是通过udp直接发送，这里服务器第一次kcp发送{KcpFlag.AllowConnectOK}
*/
///
namespace kcp
{
    internal class KcpSocketClient
    {
        

        public static string ConnectKey = "ABCDEFG0123456789";
        public int heartTime = 5;  //心跳周期
        public int relinkDelayTime = 10;    //如果重新连线，间隔多久，建议大于10秒
        uint _conv = 0;
        string remoteIp = "192.168.3.86";
        int remotePort = 11001;
        int linkcode = 0;

        //EndPoint ipep = new IPEndPoint(0, 0);
        byte[] buff = new byte[1400];
        
       
        Socket udpsocket;
        KcpClient kcpClient;


        long relinkTime;   //上次重连时间
        int connectStat = 0;   //0:创建，-1：请求分配conv,-2：连接服务端，并创建kcp。，1：连接完成 , -100:发生其他问题
        
        long nexthearttime;
        long lasthearttime;
        long lasthearttimeBack;
        public void Create(string _ip,int _port)
        {
            remoteIp = _ip;
            remotePort = _port;
            connectStat = 0;
            lasthearttimeBack = 0;

            var remote = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            //uint IOC_IN = 0x80000000;
            //uint IOC_VENDOR = 0x18000000;
            //uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            //udpsocket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpsocket.Connect(remote);

            //kcpClient = new KcpClient();
            //kcpClient.Create(this, 1);

            BeginUpdate();
        }

        

        public void output(uint _convId, byte[] _buff, int len)
        {
            if (_convId != _conv)
            {
                Console.WriteLine("数据可能错误:" + _convId + "," + _conv + ",len:" + len);
                return;
            }
            udpsocket.Send(_buff, 0, len, SocketFlags.None);
            Console.WriteLine("client output:" + len);
            //Console.WriteLine("client socket发送:" + Encoding.UTF8.GetString(_buff, 0, len));
        }


        


        async void BeginUpdate()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);

                    CheckSocketLinkStat();

                    if (kcpClient != null)
                    {
                        kcpClient.Update();
                    }


                    if (udpsocket.Available == 0)
                    {
                        continue;
                    }



                    int cnt = udpsocket.Receive(buff,SocketFlags.None,out SocketError errorCode);
                    if (errorCode != SocketError.Success)
                    {
                        Console.WriteLine("SocketError:" + errorCode.ToString());
                        Clear();
                    }
                    //每个kcp数据需要验证
                    if (cnt > 0)
                    {
                        //首先获取到conv
                        int offset = 0;
                        uint convClient = BitConverter.ToUInt32(buff, 0);
                        offset += 4;
                        if (convClient == 0)
                        {
                            ProcessUdp(buff, cnt);
                            //所有的非kcp数据接收后都不用继续走了
                            continue;
                        }

                        //走到这里的都是有conv的数据
                        if (kcpClient != null)
                            kcpClient.kcp_input(buff, cnt);
                        
                    }
                    else
                    {
                        //这里没有数据
                        Console.WriteLine("cnt:" + cnt);
                    }

                    //接收数据
                    if (kcpClient != null)
                        kcpClient.kcp_recv();

                }
            });
        }

        void Clear()
        {
            connectStat = 0;
            relinkTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            if (kcpClient != null)
            {
                kcpClient.Destory();
                kcpClient = null;
            }
        }
        
        //处理连接
        void CheckSocketLinkStat()
        {
            long nowTime;
            switch (connectStat)
            {
                case 0://发送第一次握手数据
                    //然后通知客户端conv编号再次链接
                    nowTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (nowTime - relinkTime < relinkDelayTime * 1000)
                        return;
                    relinkTime = nowTime;
                    connectStat = -1;
                    byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.ConnectRequest, ConnectKey },true,out string head);
                    udpsocket.Send(buff0, 0, buff0.Length, SocketFlags.None);
                    Console.WriteLine("发送第一次握手数据:" + Encoding.UTF8.GetString(buff0) + ",len:" + buff0.Length+","+ head);

                    break;
                case -1:
                    nowTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    if (nowTime - relinkTime < relinkDelayTime * 1000)
                        return;
                    Console.WriteLine("发送第一次握手，超时");
                    connectStat = 0;
                    relinkTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    break;
                case 1:
                    //已经成功了
                    CheckHeartBeat();
                    break;
            }
        }

        //心跳检测
        void CheckHeartBeat()
        {
            
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            
            if (now > nexthearttime)
            {
                if (lasthearttimeBack > 0 && lasthearttime - lasthearttimeBack > heartTime)
                {
                    //如果收到的心跳周期超过1个周期，那么可能掉线了。
                    Console.WriteLine("好久没接收到心跳回复，关闭连接:" + (lasthearttime - lasthearttimeBack));
                    Clear();
                    return;
                }
                lasthearttime = now;
                nexthearttime = now + heartTime;
                
                Console.WriteLine("发送心跳:" + lasthearttime + ","+DateTime.Now.ToString());
                Send(new object[] { _conv,linkcode, (int)KcpFlag.HeartBeatRequest });
            }
            
        }
        void GetHeartBack()
        {
            lasthearttimeBack = DateTimeOffset.Now.ToUnixTimeSeconds();
            Console.WriteLine("接收到服务端心跳回复...");
        }

        void ConnetOK()
        {
            lasthearttimeBack = 0;
            connectStat = 1;
            nexthearttime = DateTimeOffset.Now.ToUnixTimeSeconds() + heartTime;
            Console.WriteLine("成功连接服务端("+_conv+")...");
        }

        void ProcessUdp(byte[] _buff,int buffsize)
        {
            int index = 4; //udp数据第一位需要。
            //KcpFlag flagtype = (KcpFlag)BitConverter.ToInt32(_buff, offset);
            KcpFlag flagtype = (KcpFlag)StructConverter.ToInt32_Little2Local_Endian(_buff, index);
            index += 4;
            //为0，表示是非KCP数据,然后获取第二位，看想做什么
            switch (flagtype)
            {
                
                case KcpFlag.AllowConnectConv:
                    if (connectStat != -1)
                        break;
                    //接收到服务端发来的编号。
                    //{ 0(空),KcpFlag.AllowConnectConv(连接类型),一个随机数}
                    object[] parms = StructConverter.Unpack(StructConverter.EndianHead +"Ii", _buff, index, buffsize - index);
                    uint get_conv = (uint)parms[0];
                    //index += 4;
                    linkcode = (int)parms[1];
                    //offset += 4;
                    Console.WriteLine("linkcode:" + linkcode);
                    //再次给服务端发送，需要服务端验证自己。

                    byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.ConnectKcpRequest, get_conv, linkcode }, true, out string head);
                    udpsocket.Send(buff0, 0, buff0.Length, SocketFlags.None);
                    Console.WriteLine("发送第2次握手数据:" + Encoding.UTF8.GetString(buff0) + ",len:" + buff0.Length+","+ head);

                    //开始创建自己的kcp，开始接收数据
                    kcpClient = new KcpClient();
                    _conv = get_conv;
                    kcpClient.Create(this, get_conv);

                    connectStat = -2;
                    break;
            }

        }

        void Send(object[] parm)
        {
            byte[] buff0 = StructConverter.Pack(parm);
            Send(buff0, buff0.Length);
        }

        //KCP发送数据
        void Send(byte[] buff, int buffsize)
        {
            kcpClient.SendByte(buff, buffsize);

            Console.WriteLine("Kcp(" + _conv + ") 发送数据," + "size:" + buffsize);
        }

        //真正的接收数据
        public void KcpRecvData(byte[] _buff, int len)
        {
            Console.WriteLine(_conv + "-rec:" + Encoding.UTF8.GetString(_buff, 0, len));

            //首先获取到conv和消息类型
            object[] parms = StructConverter.Unpack(StructConverter.EndianHead + "Iii", _buff, 0, 12);
            uint con_id = (uint)parms[0];
            int con_randomkey = (int)parms[1];
            if (con_id != _conv)
            {
                Console.WriteLine("conv错误.");
                return;
            }
            if (con_randomkey != linkcode)
            {
                Console.WriteLine("linkcode错误.");
                return;
            }
            KcpFlag flag = (KcpFlag)parms[2];
            switch (flag)
            {
                case KcpFlag.AllowConnectOK:
                    ConnetOK();
                    
                    break;
                case KcpFlag.HeartBeatBack:
                    GetHeartBack();
                    
                    break;
            }
        }

    }
}
