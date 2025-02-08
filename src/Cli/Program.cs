using a2k.Cli.Commands;
using Spectre.Console.Cli;
using Status = a2k.Cli.Commands.Status;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var app = new CommandApp();
app.Configure(config =>
{
    config.ValidateExamples();
    config.UseAssemblyInformationalVersion();
    config.SetApplicationName("a2k");

    // TODO: Need to figure out logo appearing with all commands
    //AnsiConsole.Write(new FigletText("a2k").Color(Color.MediumPurple1).LeftJustified());
    //AnsiConsole.Write(new Markup("[slowblink plum4]Deploy .NET Aspire to Kubernetes![/]").LeftJustified());
    //AnsiConsole.WriteLine();
    //AnsiConsole.Write(new Rule());
    //AnsiConsole.WriteLine();

    config.AddCommand<Deploy>("deploy")
        .WithDescription("Deploy application to Kubernetes");

    config.AddCommand<Status>("status")
        .WithDescription("Show deployment status");

    config.AddCommand<Plan>("plan")
        .WithDescription("Preview deployment changes");
});

return app.Run(args);
