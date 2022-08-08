using DisControl.Bot.Attributes;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using Spectre.Console;

namespace DisControl.Bot;

public static class Discord
{
    public static DiscordClient Client;

    public static async Task Initialize()
    {
        AnsiConsole.MarkupLine("[cyan]Task: Loading bot...[/]");
        try {
            Client = new DiscordClient(new DiscordConfiguration() {
                Token = Configuration.Config.BotToken,
                TokenType = TokenType.Bot
            });
            
            var commands = Client.UseCommandsNext(new CommandsNextConfiguration {
                StringPrefixes = new[] { Configuration.Config.BotPrefix }
            });
            
            commands.CommandErrored += CommandErroredHandler;
            commands.RegisterCommands<NgrokCommands>();
            commands.RegisterCommands<VncCommands>();
            commands.RegisterCommands<Commands>();
            await Client.ConnectAsync();
        } catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
        AnsiConsole.MarkupLine("[green]Task successfully finished![/]");
    }
    
    private static async Task CommandErroredHandler(CommandsNextExtension _, CommandErrorEventArgs e)
    {
        switch (e.Exception) {
            case ChecksFailedException ex1:
                var failedChecks = ex1.FailedChecks;
                foreach (var failedCheck in failedChecks) {
                    switch (failedCheck) {
                        case AdminOnlyAttribute:
                            var embed = new DiscordEmbedBuilder()
                                .WithColor(DiscordColor.Red)
                                .WithTitle("DisControl | Error")
                                .AddField("Message", "You are not an admin!")
                                .Build();
                            await e.Context.RespondAsync(embed);
                            return;
                        case CurrentIdSetAttribute:
                            var embed1 = new DiscordEmbedBuilder()
                                .WithColor(DiscordColor.Red)
                                .WithTitle("DisControl | Error")
                                .AddField("Message", "Current ID is not set!")
                                .Build();
                            await e.Context.RespondAsync(embed1);
                            return;
                        case ParentIdSetAttribute:
                            var embed2 = new DiscordEmbedBuilder()
                                .WithColor(DiscordColor.Red)
                                .WithTitle("DisControl | Error")
                                .AddField("Message", "Parent ID is not set!")
                                .Build();
                            await e.Context.RespondAsync(embed2);
                            return;
                        case VMPoweredOnAttribute:
                            var embed3 = new DiscordEmbedBuilder()
                                .WithColor(DiscordColor.Red)
                                .WithTitle("DisControl | Error")
                                .AddField("Message", "VM is not powered on!")
                                .Build();
                            await e.Context.RespondAsync(embed3);
                            return;
                    }
                }
                return;
            case CommandNotFoundException:
                var p = Configuration.Config.BotPrefix;
                var embed4 = new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.Red)
                    .WithTitle("DisControl | Error")
                    .AddField("Message", "Unknown command!")
                    .AddField("Additional", $"Type {p}help for help.")
                    .Build();
                await e.Context.RespondAsync(embed4);
                return;
            default:
                var embed5 = new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.Red)
                    .WithTitle("DisControl | Error")
                    .AddField("Message", "An exception occured!")
                    .AddField("Additional", "Check logs for more information.")
                    .Build();
                AnsiConsole.WriteException(e.Exception);
                await e.Context.RespondAsync(embed5);
                return;
        }
    }
}