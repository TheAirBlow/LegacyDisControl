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

            var cur = Configuration.Config.CurrentId;
            var par = Configuration.Config.ParentId;
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Green)
                .WithTitle("DisControl | Started")
                .AddField("Current ID", string.IsNullOrEmpty(cur) ? "Empty" : cur)
                .AddField("Parent ID", string.IsNullOrEmpty(par) ? "Empty" : par)
                .AddField("Version", "v1.2.1")
                .Build();
            await new DiscordMessageBuilder()
                .WithEmbed(embed).SendAsync(_logs);
        } catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
        AnsiConsole.MarkupLine("[green]Task successfully finished![/]");
    }

    /// <summary>
    /// Send an embed in logs
    /// </summary>
    /// <param name="embed">Embed</param>
    public static async Task SendEmbed(DiscordEmbed embed)
        => await new DiscordMessageBuilder()
            .WithEmbed(embed).SendAsync(_logs);
}