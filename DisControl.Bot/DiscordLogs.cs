using DSharpPlus.Entities;
using Spectre.Console;

namespace DisControl.Bot;

public static class DiscordLogs
{
    private static volatile DiscordGuild _guild;
    private static volatile DiscordChannel _logs;
    
    public static async Task Initialize()
    {
        AnsiConsole.MarkupLine("[cyan]Task: Initializing logs chat...[/]");
        try {
            _guild = await Discord.Client.GetGuildAsync(Configuration.Config.GuildId);
            _logs = _guild.GetChannel(Configuration.Config.LogsChannelId);
        } catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
    }

    /// <summary>
    /// Send an embed in logs
    /// </summary>
    /// <param name="embed">Embed</param>
    public static async Task SendEmbed(DiscordEmbed embed)
        => await new DiscordMessageBuilder()
            .WithEmbed(embed).SendAsync(_logs);
}