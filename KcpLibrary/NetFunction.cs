using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLibrary
{
    public enum KcpFlag
    {
        ConnectRequest = 10,   //客户端第一次udp请求
        ConnectKcpRequest = 11, //客户端发送请求kcp连接
        AllowConnectConv = 20,    //服务端给客户端发送的conv回执，准备连接
        AllowConnectOK = 21, //服务端通过kcp发送连接成功，通知可以断
        HeartBeat = 30
    }

    internal class NetFunction
    {
    }
}
