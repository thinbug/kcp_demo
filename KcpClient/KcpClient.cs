﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static NetLibrary.KCP;
using NetLibrary;

namespace kcp
{
    unsafe internal class KcpClient
    {

        public NetLibrary.IKCPCB* kcp;
        uint userid = 0;
        //byte[] b = new byte[1400];
        byte[] kb = new byte[1400];



        //EndPoint ipep = new IPEndPoint(0, 0);
        //EndPoint remoteipep = new IPEndPoint(IPAddress.Any, 0);

        //Socket udpsocket;
        private KcpSocketClient socketClient;
        NetLibrary.d_output d_output;

        public void Create(KcpSocketClient _socketClient, uint _conv)
        {
            socketClient = _socketClient;
            userid = _conv;

            kcp = ikcp_create(userid, (void*)userid);
            d_output = new NetLibrary.d_output(udp_output);
            kcp->output = Marshal.GetFunctionPointerForDelegate(d_output);

            ikcp_wndsize(kcp, 32, 32);
            ikcp_nodelay(kcp, 1, 10, 2, 1);
            kcp->rx_minrto = 10;
            kcp->fastresend = 1;

            //udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            //udpsocket.Blocking = false;
            //udpsocket.Bind(localipep);
            Console.WriteLine("Kcp Client Created .");

        }

        public void Destory()
        {
            ikcp_release(kcp);
            Console.WriteLine("Kcp Client Destory .");
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
                    socketClient.KcpRecvData(kb, kcnt);
                }
            }
        }

        int udp_output(byte* buf, int len, NetLibrary.IKCPCB* kcp, void* user)
        {
            byte[] buff = new byte[len];
            Marshal.Copy(new IntPtr(buf), buff, 0, len);
            socketClient.output(userid, buff, len);
            //socketServer.SocketSendByte(userid, buff, len);
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
                Console.WriteLine("Kcp client SendByte:" + ret + ",size:" + len);
            }
        }

        
    }
}
