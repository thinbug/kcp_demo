
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

using static KcpLibrary.KCP;

namespace kcp_server
{
    unsafe internal class KcpClient
    {

        EndPoint ipep = new IPEndPoint(0, 0);
        byte[] b = new byte[1400];
        byte[] kb = new byte[1400];
        KcpLibrary.IKCPCB* kcp1;
        uint userid = 0;
        string localIp = "127.0.0.1";
        EndPoint remoteipep ;

        Socket udpsocket;

        public void Create(string remoteIp,int remotePort)
        {
        
            remoteipep = new IPEndPoint(IPAddress.Parse(remoteIp), remotePort);
            
            kcp1 = ikcp_create(userid, (void*)userid);

            kcp1->output = Marshal.GetFunctionPointerForDelegate(new KcpLibrary.d_output(udp_output));

            ikcp_wndsize(kcp1, 128, 128);
            ikcp_nodelay(kcp1, 1, 10, 2, 1);
            kcp1->rx_minrto = 10;
            kcp1->fastresend = 1;

            udpsocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            udpsocket.Blocking = false;
            udpsocket.Connect(remoteipep);
            Console.WriteLine("Create client !");
        }

        int udp_output(byte* buf, int len, KcpLibrary.IKCPCB* kcp, void* user)
        {
            byte[] buff = new byte[len];
            Marshal.Copy(new IntPtr(buf), buff, 0, len);
            udpsocket.SendTo(buff, 0, len, SocketFlags.None, remoteipep);
            Console.WriteLine("udp_output:" + (int)user);
            return 0;
        }

        public void Send(string txt)
        {
            var buff = Encoding.UTF8.GetBytes(txt);
            fixed (byte* p = &buff[0])
            {
                var ret = ikcp_send(kcp1, p, buff.Length);
                Console.WriteLine("server send:" + ret);
            }
        }

    }
}
