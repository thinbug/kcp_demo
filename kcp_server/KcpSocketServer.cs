using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace kcp_server
{
    internal class KcpSocketServer
    {
        public class KcpClientInfo
        {
            //此类是连接的客户端的数据。
            public uint conv;   //玩家的conv
            public string ip;   //ip
            public int port;    //端口
            public int lastHeart;   //上次心跳时间戳

            public KcpServer kcp;   //对应的客户端kcp
        }

        public static string ConnetKey = "ABCDEF0123456789";

        Socket udpsocket;
        Dictionary<uint, KcpClientInfo> kcpClientDict;

        string localIp = "127.0.0.1";

        EndPoint ipep = new IPEndPoint(0, 0);
        byte[] b = new byte[1400];
        byte[] kb = new byte[1400];
        public void Create()
        {
            kcpClientDict = new Dictionary<uint, KcpClientInfo>();
            var localipep = new IPEndPoint(IPAddress.Parse(localIp), 0);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            udpsocket.Bind(localipep);

            BeginUpdate();
        }

        async void BeginUpdate()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    //kcpClientDict.TryGetValue("key")
                    var list = kcpClientDict.GetEnumerator();
                    while (list.MoveNext())
                    {
                        var item = list.Current;
                        item.Value.kcp.Update();
                    }
                    list.Dispose();

                    if (udpsocket.Available == 0)
                    {
                        return;
                    }


                    int cnt = udpsocket.ReceiveFrom(b, ref ipep);
                    if (cnt > 0)
                    {
                        uint convClient = BitConverter.ToUInt32(b, 0);
                        bool getone = kcpClientDict.TryGetValue(k, out var kcp);
                        if (!getone)
                        {
                            kcp = new KcpServer();
                            kcpClientDict.Add(k, kcp);
                        }
                        Console.WriteLine("ReceiveFrom:" + ipep.ToString());
                        
                        kcp.kcp_input(b, cnt);
                        
                    }
                    else
                    {
                        Console.WriteLine("cnt:" + cnt);
                    }

                    kcp.Update();
                    await Task.Delay(10);
                }
            });
        }
    }
}
