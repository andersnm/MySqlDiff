using McMaster.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Text;

namespace MySqlDiff.Commands
{
    class CopyCommand
    {
        public static void RegisterWithApp(CommandLineApplication app)
        {
            app.Command("copy", cmdApp =>
            {
                cmdApp.Description = "Copies a project to a directory. Used to dump the schema of a database (using --source db)." + Environment.NewLine +
                    "The output directory can be compared using the diff command.";

                var sourceOptions = DiffCommand.RegisterProjectOptions(cmdApp, "source");
                var optionOutputPath = cmdApp.Option("--output <PATH>", "Where to save the output", CommandOptionType.SingleValue);

                cmdApp.OnExecute(() =>
                {
                    var sourceProject = DiffCommand.GetProjectFromArguments(cmdApp, "source", sourceOptions);
                    if (sourceProject == null)
                    {
                        return 1;
                    }

                    if (string.IsNullOrEmpty(optionOutputPath.Value()))
                    {
                        Console.WriteLine($"--output must be specified");
                        cmdApp.ShowHelp();
                        return 2;
                    }

                    DbProjectFileSystem.SaveAsNewDirectory(sourceProject, optionOutputPath.Value());
                    return 0;
                });
            });
        }
    }
}
