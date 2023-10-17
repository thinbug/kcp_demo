 * 握手过程 ,第一位是0表示非kcp包体。
 * 如果客户端连接，需要发送，{0(空数据),KcpFlag.ConnectRequest(连接类型),ConnectKey(连接密钥)}
 * 服务端根据连接类型，判断需要连接，返回给客户端conv编号,返回格式为{0(空),KcpFlag.AllowConnectConv(连接类型),一个随机数}
 * 客户端收到conv编号，创建kcp，并连接服务端，发送udp数据{0,KcpFlag.ConnectKcpRequest,自己的conv编号，服务端的随机数}
 * 服务端再次收到后，验证没问题就发送kcp连接成功。前面的都是通过udp直接发送，这里服务器第一次kcp发送{KcpFlag.AllowConnectOK}

注意：为了保持NAT映射，UDP需要每隔60秒就向服务器ping一次。同时为了防止出口地址改变（NAT映射改变，或者移动设备切换基站），可以使用重连，或者UDP重绑定（但是在3G,2G,EDGE下面，出口改变，TCP断了，所以简单重连也没有问题）。