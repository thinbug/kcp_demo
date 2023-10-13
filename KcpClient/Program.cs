using kcp;
using static KcpLibrary.KCP;



KcpSocketClient server = new KcpSocketClient();
server.Create("127.0.0.1",11001);
Task.Run(async () =>
{
    while (true)
    {

        await Task.Delay(10);
    }
});


Console.ReadLine();
Console.ReadLine();