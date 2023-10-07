// See https://aka.ms/new-console-template for more information
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using kcp_server;

KcpServer server = new KcpServer();
Task.Run(async () =>
{
    while (true)
    {
        server.kcp.Update(DateTimeOffset.UtcNow);
        await Task.Delay(10);
    }
});
StartRecv(kcpClient);
async void StartRecv(SimpleKcpClient client)
{
    while (true)
    {
        var res = await client.ReceiveAsync();
        var str = System.Text.Encoding.UTF8.GetString(res);
        if ("发送一条消息" == str)
        {
            Console.WriteLine(str);

            var buffer = System.Text.Encoding.UTF8.GetBytes("回复一条消息");
            client.SendAsync(buffer, buffer.Length);
        }
    }
}
Console.ReadLine();

