
using KcpServer;



KcpSocketServer server = new KcpSocketServer();
server.Create();
//Task.Run(async () =>
//{
    while (true)
    {
        
        await Task.Delay(10);
    }
//});

//Console.WriteLine("运行完后不退出窗口");
//Console.ReadKey();

