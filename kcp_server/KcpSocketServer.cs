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
 * 服务端根据连接类型，判断需要连接，返回给客户端conv编号,返回格式为{0(空),KcpFlag.AllowConnectConv(连接类型),一个随机数}
 * 客户端收到conv编号，创建kcp，并连接服务端，发送udp数据{0,KcpFlag.ConnectKcpRequest,自己的conv编号，服务端的随机数}
 * 服务端再次收到后，验证没问题就发送kcp连接成功。前面的都是通过udp直接发送，这里服务器第一次kcp发送{KcpFlag.AllowConnectOK}
*/
///
namespace kcp
{
    internal class KcpSocketServer
    {
        enum KcpFlag
        {
            ConnectRequest = 10,   //客户端第一次udp请求
            ConnectKcpRequest = 11,  
            
            
            ErrorConv = -1, //找不到这个conv了
            ErrorIPPortWrong = -2, //这个ip发生变动，可能是虚假数据

            AllowConnectConv = 20,    //服务端给客户端发送的conv回执，准备连接
            AllowConnectOK = 21, //服务端通过kcp发送连接成功，通知可以断
        }
        public class KcpClientInfo
        {
            //此类是连接的客户端的数据。
            public long createtime;  //创建时间
            public uint conv;   //玩家的conv
            public string ip;   //ip
            public int port;    //端口
            public long hearttime;   //上次心跳时间戳
            public int linkrandomcode;    //连接的随机码

            public KcpServer kcp;   //对应的客户端kcp
            public EndPoint ep;   //客户端的ep
        }

        public static string ConnectKey = "ABCDEF0123456789";
        public uint convNext
        {
            get { _conv++;return _conv; }
        }
        uint _conv = 0;
        string localIp = "127.0.0.1";
        int localPort = 11001;

        EndPoint ipep = new IPEndPoint(0, 0);
        byte[] buff = new byte[1400];
        byte[] buffKcp = new byte[1400];

        byte[] linkbuff = new byte[128];
        Socket udpsocket;
        Dictionary<uint, KcpClientInfo> kcpClientDict;
        Dictionary<uint, KcpClientInfo> kcpClientLinking;


        public void Create()
        {
            kcpClientDict = new Dictionary<uint, KcpClientInfo>();
            kcpClientLinking = new Dictionary<uint, KcpClientInfo>();
            var localipep = new IPEndPoint(IPAddress.Parse(localIp), localPort);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            udpsocket.Bind(localipep);

            BeginUpdate();
        }

        //bool IsAvailable(uint _conv)
        //{
        //    bool getone = kcpClientDict.TryGetValue(_conv, out var kcpinfo);
        //    if (!getone)
        //    {
        //        kcp = new KcpServer();
        //        kcpClientDict.Add(k, kcp);
        //    }
        //}

        public void SocketSendByte(uint _convId, byte[] _buff, int len)
        {
            bool getone = kcpClientDict.TryGetValue(_convId, out var kcpinfo);
            if (!getone)
            {
                Console.WriteLine("找不到发送对象:" + _convId);
                return;
            }
            udpsocket.SendTo(_buff, 0, len, SocketFlags.None, kcpinfo.ep);

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

                    var list = kcpClientDict.GetEnumerator();
                    while (list.MoveNext())
                    {
                        var item = list.Current;
                        item.Value.kcp.Update();
                    }
                    list.Dispose();

                    if (udpsocket.Available == 0)
                    {
                        continue;
                    }


                    int cnt = udpsocket.ReceiveFrom(buff, ref ipep);
                    //每个kcp数据需要验证
                    string ip = ((IPEndPoint)ipep).Address.ToString();
                    int port = ((IPEndPoint)ipep).Port;
                    if (cnt > 0)
                    {
                        //首先获取到conv
                        int offset = 0;
                        uint convClient = BitConverter.ToUInt32(buff, 0);
                        offset += 4;
                        if (convClient == 0)
                        {
                            ProcessUdp(ip, port, buff, cnt);
                            //所有的非kcp数据接收后都不用继续走了
                            continue;
                        }

                        //走到这里的都是有conv的数据
                        Console.WriteLine("ReceiveFrom:" + ipep.ToString());
                        bool getkcp = kcpClientDict.TryGetValue(convClient, out var info);
                        if (getkcp)
                        {
                            //验证IP和端口
                            if (info.port == port && ip.Equals(info.ip))
                            {
                                info.kcp.kcp_input(buff, cnt);
                            }
                            else
                            {
                                SocketFlagSend(KcpFlag.ErrorIPPortWrong, ipep);
                            }
                        }
                        else
                        {
                            SocketFlagSend(KcpFlag.ErrorConv, ipep);
                        }
                    }
                    else
                    {
                        //这里没有数据
                        Console.WriteLine("cnt:" + cnt);
                    }

                    //接收数据
                    list = kcpClientDict.GetEnumerator();
                    while (list.MoveNext())
                    {
                        var item = list.Current;
                        item.Value.kcp.kcp_recv();
                    }
                    list.Dispose();
                    
                    
                }
            });
        }

        void ProcessUdp(string _ip,int _port, byte[] _buff,int size)
        {
            int offset = 4; //udp数据第一位需要。
            //客户端连接，需要发送，{ 0(空数据),KcpFlag.Connect(连接类型),ConnectKey(连接密钥)}
            KcpFlag flagtype = (KcpFlag)BitConverter.ToInt32(_buff, 4);
            offset += 4;
            //为0，表示是非KCP数据,然后获取第二位，看想做什么
            switch (flagtype)
            {
                case KcpFlag.ConnectRequest:
                    //如果是连接，需要验证连接密匙
                    //{0(空数据),KcpFlag.ConnectRequest(连接类型),ConnectKey(连接密钥)}
                    string ckey = Encoding.UTF8.GetString(_buff, 8, size - offset);
                    if (ckey.Equals(ConnectKey))
                    {
                        //如果是合法的，那么需要生成一个conv。
                        uint newConv = convNext;
                        KcpClientInfo kinfo = new KcpClientInfo();
                        kinfo.conv = newConv;
                        kinfo.createtime = DateTimeOffset.Now.ToUnixTimeSeconds();
                        kinfo.ip = _ip;
                        kinfo.port = _port;
                        kinfo.hearttime = kinfo.createtime;
                        kinfo.linkrandomcode = new Random().Next(10000,20000);
                        kcpClientLinking.Add(newConv, kinfo);

                        //然后通知客户端conv编号再次链接
                        //SocketFlagSend(KcpFlag.AllowConnectConv, ipep);
                        //{ 0(空),KcpFlag.AllowConnectConv(连接类型),一个随机数}
                        //byte[] zeroUnit = BitConverter.GetBytes((int)0);
                        //zeroUnit.CopyTo(linkbuff, 0);
                        //byte[] convUnit = BitConverter.GetBytes((int)KcpFlag.AllowConnectConv);
                        //convUnit.CopyTo(linkbuff, 4);
                        //byte[] convCode = BitConverter.GetBytes(kinfo.linkrandomcode);
                        //convCode.CopyTo(linkbuff, 8);

                        byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.AllowConnectConv, kinfo.linkrandomcode });
                        //udpsocket.Send(buff0, 0, buff0.Length, SocketFlags.None, ipep);


                        udpsocket.SendTo(buff0, 0, buff0.Length, SocketFlags.None, ipep);

                        Console.WriteLine("接收到客户端第一次请求连接:" + ipep.ToString());
                        //客户端直接可以使用kcp来链接了，第一个编号必须还是0
                    }
                    break;
                case KcpFlag.ConnectKcpRequest:
                    //如果客户端发送请求，需要验证
                    //{0,KcpFlag.ConnectKcpRequest,自己的conv编号，服务端的随机数}
                    uint get_conv = BitConverter.ToUInt32(_buff, offset);
                    offset += 4;
                    int get_code = BitConverter.ToInt32(_buff, offset);
                    offset += 4;
                    //根据连接conv，识别上次的请求是否和这个请求匹配
                    bool getlinkone = kcpClientLinking.TryGetValue(get_conv, out KcpClientInfo linkinfo);
                    if (getlinkone)
                    {
                        //验证IP和端口
                        if (get_code == linkinfo.linkrandomcode && linkinfo.port == _port && _ip.Equals(linkinfo.ip))
                        {
                            //成功握手了。
                            //把连接数据放入正式数据
                            linkinfo.ep = ipep;
                            linkinfo.kcp = new KcpServer();
                            linkinfo.kcp.Create(this, linkinfo.conv);
                            linkinfo.hearttime = DateTimeOffset.Now.ToUnixTimeSeconds();
                            kcpClientDict.Add(get_conv, linkinfo);
                            kcpClientLinking.Remove(get_conv);

                            //通知客户端成功,通过kcp发送
                            byte[] convUnit = BitConverter.GetBytes((int)KcpFlag.AllowConnectOK);
                            convUnit.CopyTo(linkbuff, 4);
                            linkinfo.kcp.SendByte(linkbuff, 4);
                        }
                    }

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
