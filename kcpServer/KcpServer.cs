﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static NetLibrary.KCP;

namespace KcpServer
{
    unsafe internal class KcpServer
    {

        public NetLibrary.IKCPCB* kcp;
        uint userid = 0;
        //byte[] b = new byte[1400];
        byte[] kb = new byte[1400];



        //EndPoint ipep = new IPEndPoint(0, 0);
        //EndPoint remoteipep = new IPEndPoint(IPAddress.Any, 0);

        //Socket udpsocket;
        private KcpSocketServer socketServer;
        NetLibrary.d_output d_output;

        public void Create(KcpSocketServer _socketServer,uint _conv)
        {
            socketServer = _socketServer;
            //Console.WriteLine("Hello, World!");
            //udpsocket = _udpsocket;


            userid = _conv;

            kcp = ikcp_create(userid, (void*)userid);
            d_output = new NetLibrary.d_output(udp_output);
            kcp->output = Marshal.GetFunctionPointerForDelegate(d_output);

            ikcp_wndsize(kcp, 128, 128);
            ikcp_nodelay(kcp, 1, 10, 2, 1);
            kcp->rx_minrto = 10;
            kcp->fastresend = 1;

            //udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //udpsocket.Blocking = false;
            //udpsocket.Bind(localipep);
            Console.WriteLine("create a kcp ");

        }

      

        public void Update()
        {
            
            ikcp_update(kcp, (uint)Environment.TickCount);
            
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
            //这里需要连续接收,直到没有
            while (true)
            {
                fixed (byte* p = &kb[0])
                {
                    var kcnt = ikcp_recv(kcp, p, kb.Length);
                    if (kcnt < 0) break;
                    socketServer.KcpRecvData(userid, kb, kcnt);
                }
            }
        }

        int udp_output(byte* buf, int len, NetLibrary.IKCPCB* kcp, void* user)
        {
            //Console.WriteLine("udp_output:" + (int)user);
            byte[] buff = new byte[len];
            Marshal.Copy(new IntPtr(buf), buff, 0, len);
            socketServer.output(userid, buff, len);
            //socketServer.SocketSendByte((int)user, buff, len);
            //udpsocket.SendTo(buff, 0, len, SocketFlags.None, remoteipep);
            
            return 0;
        }

        public void SendByte(byte[] buff, int len)
        {
            fixed (byte* p = &buff[0])
            {
                var ret = ikcp_send(kcp, p, len);
                if (ret != 0)
                {
                    Console.WriteLine("Kcp server SendByte Error:" + ret + ",size:" + len);
                }
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
