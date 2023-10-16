// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
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

