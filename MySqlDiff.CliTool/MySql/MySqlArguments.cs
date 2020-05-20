namespace MySqlDiff.CliTool.MySql
{
    public class MySqlArguments
    {
        public bool UseSshTunnel { get; set; }
        public string ConnectionString { get; set; }
        public string SshHost { get; set; }
        public string SshUserName { get; set; }
        public string SshPassword { get; set; }
        public uint SshPort { get; set; } = 3306;
    }
}
