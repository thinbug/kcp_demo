using NetLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

//说明

///
/*
 * 握手过程 ,第一位是0表示非kcp包体。
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
        public class KcpClientInfo
        {
            //此类是连接的客户端的数据。
            public long createtime;  //创建时间
            public uint conv;   //玩家的conv

            public long hearttime;   //上次心跳时间戳
            public int linkrandomcode;    //连接的随机码

            public KcpServer kcp;   //对应的客户端kcp
            public EndPoint ep;   //客户端的ep
        }

        public static string ConnectKey = "ABCDEFG0123456789";
        public int TimeOutLink = 3;  //握手超时认为掉线了。需要清除
        public int TimeOutHeart = 30;  //超时认为掉线了。需要清除

        public uint convNext
        {
            get { _convNow++; return _convNow; }
        }
        uint _convNow = 0;
        string localIp = null;
        int localPort = 11001;

        EndPoint ipep = new IPEndPoint(0, 0);
        byte[] buff = new byte[1400];

        Socket udpsocket;
        Dictionary<uint, KcpClientInfo> kcpClientDict;
        Dictionary<uint, KcpClientInfo> kcpClientLinking;


        public void Create()
        {
            Console.WriteLine("Server - IsLittleEndian:" + BitConverter.IsLittleEndian);
            kcpClientDict = new Dictionary<uint, KcpClientInfo>();
            kcpClientLinking = new Dictionary<uint, KcpClientInfo>();

            IPAddress ip = IPAddress.Any;
            if(!string.IsNullOrEmpty(localIp))
                ip = IPAddress.Parse(localIp);

            var localipep = new IPEndPoint(ip, localPort);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            udpsocket.Bind(localipep);

            BeginUpdate();


        }


        public void output(uint _convId, byte[] _buff, int len)
        {
            bool getone = kcpClientDict.TryGetValue(_convId, out var kcpinfo);
            if (!getone)
            {
                Console.WriteLine("找不到发送对象:" + _convId + "," + _convId + ",len:" + len);
                return;
            }
            udpsocket.SendTo(_buff, 0, len, SocketFlags.None, kcpinfo.ep);

            //Console.WriteLine("socket发送:" + _convId);
        }


        async void BeginUpdate()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(10);
                    
                    long nowhearttime = DateTimeOffset.Now.ToUnixTimeSeconds();

                    //链接超时
                    var listlink = kcpClientLinking.GetEnumerator();
                    while (listlink.MoveNext())
                    {
                        var item = listlink.Current;
                        if (nowhearttime - item.Value.createtime > TimeOutLink)
                        {
                            Console.WriteLine("Link超时了需要删除:" + item.Value.conv);
                            kcpClientLinking.Remove(item.Key);
                        }
                    }
                    listlink.Dispose();

                    var list = kcpClientDict.GetEnumerator();
                    while (list.MoveNext())
                    {
                        var item = list.Current;
                        item.Value.kcp.Update();

                        if (nowhearttime - item.Value.hearttime > TimeOutHeart)
                        {
                            Console.WriteLine("超时了需要删除:"+item.Value.conv);
                            kcpClientDict.Remove(item.Key);
                        }
                    }
                    list.Dispose();

                    if (udpsocket.Available == 0)
                    {
                        continue;
                    }


                    int cnt = udpsocket.ReceiveFrom(buff, ref ipep);
                    //每个kcp数据需要验证
                    //string ip = ((IPEndPoint)ipep).Address.ToString();
                    //int port = ((IPEndPoint)ipep).Port;
                    if (cnt > 0)
                    {
                        //Console.WriteLine("cnt:" + cnt);

                        //首先获取到conv
                        int offset = 0;
                        uint convClient = StructConverter.ToUInt32_B2L_Endian(buff, offset);
                        offset += 4;
                        if (convClient == 0)
                        {
                            ProcessUdp(ipep, buff, cnt);
                            //所有的非kcp数据接收后都不用继续走了
                            continue;
                        }


                        //走到这里的都是有conv的数据
                        //Console.WriteLine("Receive KCP From:" + ipep.ToString() + ",buffsize:"+ cnt);
                        bool getkcp = kcpClientDict.TryGetValue(convClient, out var info);
                        if (getkcp)
                        {


                            //验证IP和端口
                            if (ipep.Equals(info.ep))
                            {
                                //进入input会发送ack数据
                                info.kcp.kcp_input(buff, cnt);
                            }
                            else
                            {
                                Console.WriteLine("IP Port 不匹配");
                                //SocketFlagSend(KcpFlag.ErrorIPPortWrong, ipep);
                            }

                        }
                        else
                        {
                            Console.WriteLine("未知的conv编号：" + convClient);
                            //SocketFlagSend(KcpFlag.ErrorConv, ipep);
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

        void ProcessUdp(EndPoint ep, byte[] _buff, int buffsize)
        {
            int index = 4; //udp数据第一位需要。
                           //客户端连接，需要发送，{ 0(空数据),KcpFlag.Connect(连接类型),。。。。。}

            KcpFlag flagtype = (KcpFlag)StructConverter.ToInt32_Little2Local_Endian(_buff, index);
            index += 4;
            //为0，表示是非KCP数据,然后获取第二位，看想做什么
            object[] parms;
            switch (flagtype)
            {
                case KcpFlag.ConnectRequest:
                    //如果是连接，需要验证连接密匙
                    //{0(空数据),KcpFlag.ConnectRequest(连接类型),ConnectKey(连接密钥)}
                    parms = StructConverter.Unpack(StructConverter.EndianHead + ConnectKey.Length + "s", _buff, index, buffsize - index);
                    string ckey = (string)parms[0];// Encoding.UTF8.GetString(_buff, ConnectKey.Length, size - offset);
                    if (ckey.Equals(ConnectKey))
                    {
                        //如果是合法的，那么需要生成一个conv。
                        uint newConv = convNext;
                        KcpClientInfo kinfo = new KcpClientInfo();
                        kinfo.conv = newConv;
                        kinfo.createtime = DateTimeOffset.Now.ToUnixTimeSeconds();
                        kinfo.ep = ep;
                        kinfo.hearttime = kinfo.createtime;
                        kinfo.linkrandomcode = new Random().Next(10000, int.MaxValue);
                        kcpClientLinking.Add(newConv, kinfo);
                        //Console.WriteLine("linkcode:" + kinfo.linkrandomcode);
                        //然后通知客户端conv编号再次链接

                        byte[] buff0 = StructConverter.Pack(new object[] { (int)0, (int)KcpFlag.AllowConnectConv, newConv, kinfo.linkrandomcode });
                        udpsocket.SendTo(buff0, 0, buff0.Length, SocketFlags.None, ipep);

                        Console.WriteLine("接收到客户端第一次请求连接:" + ipep.ToString());
                        //客户端直接可以使用kcp来链接了，第一个编号必须还是0
                    }
                    break;
                case KcpFlag.ConnectKcpRequest:
                    //如果客户端发送请求，需要验证
                    //{0,KcpFlag.ConnectKcpRequest,自己的conv编号，服务端的随机数}
                    //uint get_conv = BitConverter.ToUInt32(_buff, index);
                    //index += 4;
                    //int get_code = BitConverter.ToInt32(_buff, index);
                    //index += 4;
                    parms = StructConverter.Unpack(StructConverter.EndianHead + "Ii", _buff, index, buffsize - index);
                    uint get_conv = (uint)parms[0];
                    int get_code = (int)parms[1];
                    //根据连接conv，识别上次的请求是否和这个请求匹配
                    bool getlinkone = kcpClientLinking.TryGetValue(get_conv, out KcpClientInfo linkinfo);
                    if (getlinkone)
                    {
                        //验证IP和端口
                        if (get_code == linkinfo.linkrandomcode && linkinfo.ep.Equals(ipep))
                        {
                            //成功握手了。
                            //把连接数据放入正式数据
                            Console.WriteLine("同意客户端请求连接:" + ipep.ToString());

                            linkinfo.ep = ipep;
                            linkinfo.kcp = new KcpServer();
                            linkinfo.kcp.Create(this, linkinfo.conv);
                            linkinfo.hearttime = DateTimeOffset.Now.ToUnixTimeSeconds();
                            kcpClientDict.Add(get_conv, linkinfo);
                            kcpClientLinking.Remove(get_conv);

                            //通知客户端成功,通过kcp发送
                            byte[] buff0 = StructConverter.Pack(new object[] { linkinfo.conv, linkinfo.linkrandomcode, (int)KcpFlag.AllowConnectOK });
                            Send(linkinfo, buff0, buff0.Length);

                        }
                    }

                    break;
            }

        }

        void Send(KcpClientInfo client, byte[] buff, int buffsize)
        {
            client.kcp.SendByte(buff, buffsize);

            Console.WriteLine("Kcp(" + client.conv + ") 发送数据," + "size:" + buffsize);
        }


        public void KcpRecvData(uint _convId, byte[] _buff, int len)
        {
            //Console.WriteLine(_convId + "-server rec:" + Encoding.UTF8.GetString(_buff, 0, len));
            //首先获取到conv和消息类型
            object[] parms = StructConverter.Unpack(StructConverter.EndianHead + "Iii", _buff, 0, 12);
            uint con_id = (uint)parms[0];
            int con_linkcode = (int)parms[1];
            bool getlinkone = kcpClientDict.TryGetValue(con_id, out KcpClientInfo linkinfo);
            if (!getlinkone)
            {
                Console.WriteLine("conv错误:" + con_id);
                return;
            }
            if (con_linkcode != linkinfo.linkrandomcode)
            {
                Console.WriteLine("conv错误:" + con_linkcode);
                return;
            }

            KcpFlag flag = (KcpFlag)parms[2];
            switch (flag)
            {
                case KcpFlag.HeartBeat:
                    HeartBeatProc(linkinfo);
                    break;
            }
        }

        void HeartBeatProc(KcpClientInfo info)
        {
            info.hearttime = DateTimeOffset.Now.ToUnixTimeSeconds();
            Console.WriteLine("心跳接收(" + info.conv + ") :" + info.ep.ToString());
        }
    }
}
