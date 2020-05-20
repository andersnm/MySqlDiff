using System;
using Renci.SshNet;

namespace MySqlDiff
{
    class SshTunnel : IDisposable
    {
        private SshClient client;
        private ForwardedPortLocal port;

        public SshTunnel(ConnectionInfo connectionInfo, uint remotePort)
        {
            try
            {
                client = new SshClient(connectionInfo);
                port = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", remotePort);

                //client.ErrorOccurred += (s, args) => args.Dump();
                //port.Exception += (s, args) => args.Dump();
                //port.RequestReceived += (s, args) => args.Dump();

                client.Connect();
                client.AddForwardedPort(port);
                port.Start();

                LocalPort = port.BoundPort;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public uint LocalPort { get; }

        public void Dispose()
        {
            if (port != null)
                port.Dispose();
            if (client != null)
                client.Dispose();
        }
    }
}
