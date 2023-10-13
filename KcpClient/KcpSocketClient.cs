using KcpLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        enum KcpFlag
        {
            ConnectRequest = 10,   //客户端第一次udp请求
            ConnectKcpRequest = 11,     //客户端第二次发送kcp编号
            
            
            ErrorConv = -1, //找不到这个conv了
            ErrorIPPortWrong = -2, //这个ip发生变动，可能是虚假数据

            AllowConnectConv = 20,    //服务端给客户端发送的conv回执，准备连接
            AllowConnectOK = 21, //服务端通过kcp发送连接成功，通知可以断
        }
        

        public static string ConnectKey = "ABCDEF0123456789";
        
        uint _conv = 0;
        string remoteIp = "127.0.0.1";
        int remotePort = 11001;
        int linkcode = 0;

        //EndPoint ipep = new IPEndPoint(0, 0);
        byte[] buff = new byte[1400];
        byte[] buffKcp = new byte[1400];
        byte[] linkbuff = new byte[128];

        Socket udpsocket;
        KcpClient kcpClient;



        int stat = 0;   //0:创建，-1：请求分配conv,-2：连接服务端，并创建kcp。，1：连接完成 , -100:发生其他问题

        public void Create(string _ip,int _port)
        {
            remoteIp = _ip;
            remotePort = _port;
            stat = 0;
            var remote = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpsocket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
            udpsocket.Connect(remote);

            kcpClient = new KcpClient();
            kcpClient.Create(this, 1);

            BeginUpdate();
        }

        

        public void SocketSendByte(uint _convId, byte[] _buff, int len)
        {
            udpsocket.Send(_buff, 0, len, SocketFlags.None);
            Console.WriteLine("socket发送:" + _convId);
        }

        public void SocketRecvData(uint _convId, byte[] _buff, int len)
        {
            Console.WriteLine(_convId + "-rec:" + Encoding.UTF8.GetString(_buff, 0, len));
        }

        async void BeginUpdate()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);

                    ProcessLinkServer();

                    if (kcpClient != null)
                    {
                        kcpClient.Update();
                    }


                    if (udpsocket.Available == 0)
                    {
                        continue;
                    }



                    int cnt = udpsocket.Receive(buff);
                    //每个kcp数据需要验证
                    if (cnt > 0)
                    {
                        //首先获取到conv
                        int offset = 0;
                        uint convClient = BitConverter.ToUInt32(buff, 0);
                        offset += 4;
                        if (convClient == 0)
                        {
                            ProcessUdp(buff);
                            //所有的非kcp数据接收后都不用继续走了
                            continue;
                        }

                        //走到这里的都是有conv的数据
                        kcpClient.kcp_input(buff, cnt);
                        
                    }
                    else
                    {
                        //这里没有数据
                        Console.WriteLine("cnt:" + cnt);
                    }

                    //接收数据
                    kcpClient.kcp_recv();

                }
            });
        }

        
        //处理连接
        void ProcessLinkServer()
        {
            if (stat == 1)
                return;
            switch (stat)
            {
                case 0://发送第一次握手数据
                    //然后通知客户端conv编号再次链接
                    stat = -1;
                    byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.ConnectRequest, ConnectKey },false,out string head);
                    udpsocket.Send(buff0, 0, buff0.Length, SocketFlags.None);
                    Console.WriteLine("发送第一次握手数据:" + Encoding.UTF8.GetString(buff0) + ",len:" + buff0.Length+","+ head);

                    break;
            }
        }

        void ProcessUdp(byte[] _buff)
        {
            int offset = 4; //udp数据第一位需要。
            KcpFlag flagtype = (KcpFlag)BitConverter.ToInt32(_buff, offset);
            offset += 4;
            //为0，表示是非KCP数据,然后获取第二位，看想做什么
            switch (flagtype)
            {
                
                case KcpFlag.AllowConnectConv:
                    if (stat != -1)
                        break;
                    //接收到服务端发来的编号。
                    //{ 0(空),KcpFlag.AllowConnectConv(连接类型),一个随机数}
                    uint get_conv = BitConverter.ToUInt32(_buff, offset);
                    offset += 4;
                    linkcode = BitConverter.ToInt32(_buff, offset);
                    //offset += 4;

                    //再次给服务端发送，需要服务端验证自己。


                    //{0,KcpFlag.ConnectKcpRequest,自己的conv编号，服务端的随机数}
                    //byte[] zeroUnit = BitConverter.GetBytes((int)0);
                    //zeroUnit.CopyTo(linkbuff, 0);
                    //byte[] flagUnit = BitConverter.GetBytes((int)KcpFlag.ConnectKcpRequest);
                    //flagUnit.CopyTo(linkbuff, 4);
                    //byte[] convUnit = BitConverter.GetBytes((uint)get_conv);
                    //convUnit.CopyTo(linkbuff, 8);
                    //byte[] codeUnit = BitConverter.GetBytes((int)linkcode);
                    //codeUnit.CopyTo(linkbuff, 12);
                    //udpsocket.Send(linkbuff, 0, 16, SocketFlags.None);

                    byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.ConnectKcpRequest, get_conv, linkcode },false,out string head);
                    udpsocket.Send(buff0, 0, buff0.Length, SocketFlags.None);
                    Console.WriteLine("发送第2次握手数据:" + Encoding.UTF8.GetString(buff0) + ",len:" + buff0.Length+","+ head);

                    //开始创建自己的kcp，开始接收数据
                    kcpClient = new KcpClient();
                    kcpClient.Create(this, get_conv);

                    stat = -2;
                    break;
            }

        }

        void SocketFlagSend(KcpFlag kcpflag,EndPoint ep)
        {
            //然后通知客户端conv编号再次链接
            byte[] zeroUnit = BitConverter.GetBytes((int)0);
            zeroUnit.CopyTo(linkbuff, 0);
            byte[] convUnit = BitConverter.GetBytes((int)kcpflag);
            convUnit.CopyTo(linkbuff, 4);
            udpsocket.SendTo(linkbuff, 0, 8, SocketFlags.None, ep);
        }
    }
}
