using McMaster.Extensions.CommandLineUtils;
using MySqlDiff.Commands;

namespace MySqlDiff
{

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication()
            {
                Name = "mysqldiff",
                Description = "MySql Migration Tool",
            };

            app.HelpOption(true);

            DiffCommand.RegisterWithApp(app);
            CopyCommand.RegisterWithApp(app);

            app.OnExecute(() =>
            {
                app.ShowHelp();
            });

            return app.Execute(args);
        }
    }
}
