
using kcp;



KcpSocketServer server = new KcpSocketServer();
server.Create();
Task.Run(async () =>
{
    while (true)
    {
        
        await Task.Delay(10);
    }
});


Console.ReadLine();

