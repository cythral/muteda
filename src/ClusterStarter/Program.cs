using System.Text;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ClusterStarter
{
    class Program
    {
        static void Main(string[] args)
        {
            var port = 3306;
            var localAddr = IPAddress.Parse("0.0.0.0");
            var server = new TcpListener(localAddr, port);

            server.Start();

            while (true)
            {
                var client = server.AcceptTcpClient();
                Task.Run(() => OnConnect(client));
            }
        }

        static void OnConnect(TcpClient client)
        {
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(Encoding.UTF8.GetBytes("test"));
            client.Client.SendAsync(args);
        }
    }
}
