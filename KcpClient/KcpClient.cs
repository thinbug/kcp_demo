using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static KcpLibrary.KCP;

namespace kcp
{
    unsafe internal class KcpClient
    {

        public KcpLibrary.IKCPCB* kcp;
        uint userid = 0;
        //byte[] b = new byte[1400];
        byte[] kb = new byte[1400];



        //EndPoint ipep = new IPEndPoint(0, 0);
        //EndPoint remoteipep = new IPEndPoint(IPAddress.Any, 0);

        //Socket udpsocket;
        private KcpSocketClient socketClient;


        public void Create(KcpSocketClient _socketClient, uint _conv)
        {
            socketClient = _socketClient;
            userid = _conv;

            kcp = ikcp_create(userid, (void*)userid);

            kcp->output = Marshal.GetFunctionPointerForDelegate(new KcpLibrary.d_output(udp_output));

            ikcp_wndsize(kcp, 128, 128);
            ikcp_nodelay(kcp, 1, 10, 2, 1);
            kcp->rx_minrto = 10;
            kcp->fastresend = 1;

            //udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //udpsocket.Blocking = false;
            //udpsocket.Bind(localipep);
            Console.WriteLine("Kcp Client Created .");

        }



        public void Update()
        {
            //if (udpsocket == null || kcp == null)
            //{
            //    return;
            //}
            ikcp_update(kcp, (uint)Environment.TickCount);
            //if (udpsocket.Available == 0)
            //{
            //    return;
            //}


            //int cnt = udpsocket.ReceiveFrom(b, ref ipep);
            //if (cnt > 0)
            //{
            //    Console.WriteLine("ReceiveFrom:" + ipep.ToString());
            //    fixed (byte* p = &b[0])
            //    {
            //        ikcp_input(kcp, p, cnt);
            //    }
            //}
            //else
            //{
            //    Console.WriteLine("cnt:" + cnt);
            //}
            //fixed (byte* p = &kb[0])
            //{
            //    var kcnt = ikcp_recv(kcp, p, kb.Length);
            //    if (kcnt > 0)
            //    {
            //        Console.WriteLine("rec:" + Encoding.UTF8.GetString(kb, 0, kcnt));
            //    }
            //}
        }

        public void kcp_input(byte[] data, long size)
        {
            fixed (byte* p = &data[0])
            {
                ikcp_input(kcp, p, size);
            }


        }
        public void kcp_recv()
        {
            fixed (byte* p = &kb[0])
            {
                var kcnt = ikcp_recv(kcp, p, kb.Length);
                if (kcnt > 0)
                {
                    socketClient.SocketRecvData(userid, kb, kcnt);
                }
            }
        }

        int udp_output(byte* buf, int len, KcpLibrary.IKCPCB* kcp, void* user)
        {
            byte[] buff = new byte[len];
            Marshal.Copy(new IntPtr(buf), buff, 0, len);
            socketClient.SocketSendByte(userid, buff, len);
            //socketServer.SocketSendByte((int)user, buff, len);
            //udpsocket.SendTo(buff, 0, len, SocketFlags.None, remoteipep);
            //Console.WriteLine("udp_output:" + (int)user);
            return 0;
        }

        public void SendByte(byte[] buff, int len)
        {
            fixed (byte* p = &buff[0])
            {
                var ret = ikcp_send(kcp, p, len);
                Console.WriteLine("Kcp SendByte:" + ret);
            }
        }

        //public void Send(string txt)
        //{
        //    var buff = Encoding.UTF8.GetBytes(txt);
        //    fixed (byte* p = &buff[0])
        //    {
        //        var ret = ikcp_send(kcp, p, buff.Length);
        //        Console.WriteLine("server send:" + ret);
        //    }
        //}

    }
}
