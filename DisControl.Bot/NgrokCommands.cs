using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Spectre.Console;

namespace DisControl.Bot;

public class NgrokCommands : BaseCommandModule
{
    [Command("tunnelstart")]
    [Description("Start ngrok tunnel")]
    public async Task StartTunnel(CommandContext ctx)
    {
        if (!Configuration.Config.AdminId.Contains(ctx.Member.Id)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("You're not an admin, fuck off")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        if (!string.IsNullOrEmpty(Ngrok.Tunnel.PublicUrl)) {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "Already started!")
                .Build();
            await ctx.RespondAsync(embed);
            return;
        }
        
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Start Tunnel")
            .AddField("Status", "Starting...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        
        try { Ngrok.StartTunnel(); } 
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Unable to start ngrok tunnel![/]");
            AnsiConsole.WriteException(e);
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "Tunnel Start failed: An exception occured!")
                .AddField("Additional", "See logs for more information.")
                .Build();
            await message.ModifyAsync(embed);
            return;
        }

        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Start Tunnel")
            .AddField("Status", "Done!")
            .Build();
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | VNC Tunnel Started")
            .AddField("Description", "VNC ngrok tunnel just started!")
            .AddField("Address", Ngrok.Tunnel.PublicUrl)
            .Build());
        await message.ModifyAsync(embed4);
    }
    
    [Command("tunnelstop")]
    [Description("Stop ngrok tunnel")]
    public async Task StopTunnel(CommandContext ctx)
    {
        if (!Configuration.Config.AdminId.Contains(ctx.Member.Id)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("You're not an admin, fuck off")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        if (string.IsNullOrEmpty(Ngrok.Tunnel.PublicUrl)) {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "Not started yet!")
                .Build();
            await ctx.RespondAsync(embed);
            return;
        }
        
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Stop Tunnel")
            .AddField("Status", "Stopping...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        
        try { Ngrok.StartTunnel(); } 
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Unable to stop ngrok tunnel![/]");
            AnsiConsole.WriteException(e);
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "Tunnel Stop failed: An exception occured!")
                .AddField("Additional", "See logs for more information.")
                .Build();
            await message.ModifyAsync(embed);
            return;
        }

        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Stop Tunnel")
            .AddField("Status", "Done!")
            .Build();
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Red)
            .WithTitle("DisControl | VNC Tunnel Stopped")
            .AddField("Description", "VNC ngrok tunnel just stopped!")
            .Build());
        await message.ModifyAsync(embed4);
    }
}