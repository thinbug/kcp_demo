using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetLibrary
{
    public enum KcpFlag
    {
        ConnectRequest = 110,       //客户端第一次udp请求
        ConnectKcpRequest = 111,    //客户端发送请求kcp连接
        HeartBeatRequest = 130,     //客户端给服务端发送心跳

        AllowConnectConv = 20,      //服务端给客户端发送的conv回执，准备连接
        AllowConnectOK = 21,        //服务端通过kcp发送连接成功，通知可以断
        HeartBeatBack = 31,         //服务端给客户返回心跳消息
        
    }

    internal class NetFunction
    {
    }
}
