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
        Socket udpsocket;
        Dictionary<string, KcpServer> kcpClientDict;

        string localIp = "127.0.0.1";

        EndPoint ipep = new IPEndPoint(0, 0);
        byte[] b = new byte[1400];
        byte[] kb = new byte[1400];
        public void Create()
        {
            kcpClientDict = new Dictionary<string, KcpServer> ();
            var localipep = new IPEndPoint(IPAddress.Parse(localIp), 0);
            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            udpsocket.Bind(localipep);

            //kcp = new KcpServer();
            //kcp.Create(udpsocket);

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
                        item.Value.Update();
                    }
                    list.Dispose();

                    if (udpsocket.Available == 0)
                    {
                        return;
                    }


                    int cnt = udpsocket.ReceiveFrom(b, ref ipep);
                    if (cnt > 0)
                    {
                        string k = ipep.ToString();
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
