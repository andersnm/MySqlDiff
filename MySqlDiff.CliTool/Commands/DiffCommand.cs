using McMaster.Extensions.CommandLineUtils;
using MySqlDiff.CliTool.MySql;
using System;
using System.Collections.Generic;
using System.Text;

namespace MySqlDiff.Commands
{
    public class DbProjectCmdArguments
    {
        public CommandOption Type { get; set; }
        public CommandOption Path { get; set; }
        public CommandOption ConnectionString { get; set; }
        public CommandOption SshHost { get; set; }
        public CommandOption SshPort { get; set; }
        public CommandOption SshUserName { get; set; }
        public CommandOption SshPassword { get; set; }
    }

    public class DiffCommand
    {
        internal static DbProjectCmdArguments RegisterProjectOptions(CommandLineApplication app, string prefix)
        {
            return new DbProjectCmdArguments
            {
                Type = app.Option($"--{prefix} <TYPE>", "fs, db or null", CommandOptionType.SingleValue).IsRequired(),
                Path = app.Option($"--{prefix}-fs-path <PATH>", $"Project directory. Only used with --{prefix} fs", CommandOptionType.SingleValue),
                ConnectionString = app.Option($"--{prefix}-db-cs <CONNECTIONSTRING>", $"MySQL connection string. Only used with --{prefix} db", CommandOptionType.SingleValue),
                SshHost = app.Option($"--{prefix}-db-ssh-host <HOST>", $"MySQL connection SSH tunnel host. Optional. Only used with --{prefix} db", CommandOptionType.SingleValue),
                SshPort = app.Option($"--{prefix}-db-ssh-port <PORT>", $"MySQL connection SSH port. Optional. Only used with --{prefix} db", CommandOptionType.SingleValue),
                SshUserName = app.Option($"--{prefix}-db-ssh-username <USERNAME>", $"MySQL connection SSH user name. Optional. Only used with --{prefix} db", CommandOptionType.SingleValue),
                SshPassword = app.Option($"--{prefix}-db-ssh-password <USERNAME>", $"MySQL connection SSH password. Optional. Only used with --{prefix} db", CommandOptionType.SingleValue),
            };
        }

        internal static DbProject GetProjectFromArguments(CommandLineApplication cmdApp, string prefix, DbProjectCmdArguments args)
        {
            if (args.Type.Value() == "fs")
            {
                if (string.IsNullOrEmpty(args.Path.Value()))
                {
                    Console.WriteLine($"--{prefix}-fs-path must specify the input directory");
                    cmdApp.ShowHelp();
                    return null;
                }

                return DbProjectFileSystem.CreateFromDirectory(args.Path.Value());
            }
            else if (args.Type.Value() == "db")
            {
                if (string.IsNullOrEmpty(args.ConnectionString.Value()))
                {
                    Console.WriteLine($"--{prefix}-db-cs must specify the MySQL connection string");
                    cmdApp.ShowHelp();
                    return null;
                }

                var dbInfo = new MySqlArguments()
                {
                    ConnectionString = args.ConnectionString.Value(),
                    UseSshTunnel = args.SshHost.Values.Count > 0,
                    SshHost = args.SshHost.Value(),
                    SshPort = uint.Parse(args.SshHost.Value()),
                    SshUserName = args.SshUserName.Value(),
                    SshPassword = args.SshPassword.Value(),
                };

                return DbProjectMySql.CreateFromDatabase(dbInfo);
            }
            else if (args.Type.Value() == "null") {
                return new DbProject()
                {
                    Statements = new List<Statement>(),
                };
            }
            else
            {
                Console.WriteLine($"Expected --{prefix} fs or --{prefix} db or --{prefix} null");
                cmdApp.ShowHelp();
                return null;
            }
        }

        public static void RegisterWithApp(CommandLineApplication app)
        {
            app.Command("diff", cmdApp =>
            {
                cmdApp.Description = "Generates SQL with the difference between the source and target projects." + Environment.NewLine +
                    "Used to generate up/down migration SQL, or SQL to rebuild a database from scratch (using --source null)." + Environment.NewLine + 
                    "The generated SQL can be executed by the MySQL command line client.";

                var sourceOptions = RegisterProjectOptions(cmdApp, "source");
                var targetOptions = RegisterProjectOptions(cmdApp, "target");

                cmdApp.OnExecute(() =>
                {
                    var sourceProject = GetProjectFromArguments(cmdApp, "source", sourceOptions);
                    if (sourceProject == null)
                    {
                        return 1;
                    }

                    var targetProject = GetProjectFromArguments(cmdApp, "target", targetOptions);
                    if (targetProject == null)
                    {
                        return 2;
                    }

                    var sql = DatabaseDiff.Diff(targetProject.Statements, sourceProject.Statements);
                    Console.WriteLine(sql);
                    
                    return 0;
                });
            });
        }
    }
}
