using kcp;
using static NetLibrary.KCP;



KcpSocketClient server = new KcpSocketClient();
server.Create("127.0.0.1", 11001);
//server.Create("112.126.169.192", 11001);

while (true)
{

    await Task.Delay(10);
}