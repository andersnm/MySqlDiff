using System;
using MySql.Data.MySqlClient;
using Renci.SshNet;

namespace MySqlDiff.CliTool.MySql
{
    public class MySqlContext : IDisposable
    {
        internal MySqlConnection Connection { get; set; }
        internal SshTunnel Tunnel { get; set; }

        public MySqlContext(MySqlArguments dbInfo)
        {
            var connectionString = dbInfo.ConnectionString;
            if (!string.IsNullOrEmpty(dbInfo.SshHost))
            {
                var ci = new ConnectionInfo(dbInfo.SshHost, dbInfo.SshUserName, new PasswordAuthenticationMethod(dbInfo.SshUserName, dbInfo.SshPassword));
                Tunnel = new SshTunnel(ci, dbInfo.SshPort);

                connectionString += dbInfo.ConnectionString + ";port=" + Tunnel.LocalPort.ToString();
            }

            Connection = new MySqlConnection(connectionString);
        }

        public void Dispose()
        {
            Connection?.Dispose();
            Connection = null;
            Tunnel?.Dispose();
            Tunnel = null;
        }
    }

}
