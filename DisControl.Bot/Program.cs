using Spectre.Console;

namespace DisControl.Bot;

public static class Program
{
    public static async Task Main(string[] args)
    {
        AnsiConsole.MarkupLine("[green]Welcome to DisControl v1.2.1 by TheAirBlow![/]");
        Configuration.LoadConfiguration(); VMware.Initialize(); DataSet.Initialize();
        await Discord.Initialize(); await DiscordLogs.Initialize(); 
        await Task.Delay(-1);
    }
}