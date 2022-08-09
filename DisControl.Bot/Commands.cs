using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Spectre.Console;

namespace DisControl.Bot;

/// <summary>
/// VMware Management commands
/// </summary>
public class Commands : BaseCommandModule
{
    [Command("autorestart")] [Aliases("ar")]
    [Description("Current VM's information")]
    public async Task AutoRestartMethod(CommandContext ctx, [Description("Enable or disable")] bool enabled)
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

        if (string.IsNullOrEmpty(Configuration.Config.CurrentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Current VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        if (enabled) AutoRestart.Start();
        else AutoRestart.Stop();

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Auto Restart")
            .WithDescription("Successfully started/stopped the thread!")
            .Build();
        await ctx.RespondAsync(embed2);
    }

    [Command("info")] [Aliases("iw")]
    [Description("Current VM's information")]
    public async Task Info(CommandContext ctx)
    {
        if (string.IsNullOrEmpty(Configuration.Config.CurrentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Current VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Information")
            .AddField("Status", "Fetching...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        
        var power = "";
        var color = DiscordColor.Aquamarine;
        switch (VMware.GetState()) {
            case VMware.PowerState.paused:
                color = DiscordColor.Yellow;
                power = "Paused";
                break;
            case VMware.PowerState.suspended:
                color = DiscordColor.Yellow;
                power = "Suspended";
                break;
            case VMware.PowerState.poweredOff:
                color = DiscordColor.Red;
                power = "Powered OFF";
                break;
            case VMware.PowerState.poweredOn:
                color = DiscordColor.Green;
                power = "Powered ON";
                break;
        }

        var ngrok = string.IsNullOrEmpty(Ngrok.Tunnel.PublicUrl)
            ? "Not started yet!" : Ngrok.Tunnel.PublicUrl;
        var embed = new DiscordEmbedBuilder()
            .WithColor(color)
            .WithTitle("DisControl | Information")
            .AddField("VM's ID",
                Configuration.Config.CurrentId)
            .AddField("Ngrok VNC", ngrok)
            .AddField("Power", power)
            .AddField("Status", "Done!")
            .Build();
        await message.ModifyAsync(embed);
    }
    
    [Command("setstate")] [Aliases("ss")]
    [Description("Sets power state of Current ID")]
    public async Task SetState(CommandContext ctx, [Description("VM Power State (on, off, pause, suspend)")] string state)
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

        if (string.IsNullOrEmpty(Configuration.Config.CurrentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Current VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Change State")
            .AddField("Status", 
                "Setting power state...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        
        try { await VMware.SetState(state); } 
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Unable to set VM's state![/]");
            AnsiConsole.WriteException(e);
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "VM State Change failed: An exception occured!")
                .AddField("Additional", "See logs for more information.")
                .Build();
            await message.ModifyAsync(embed);
            return;
        }

        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Change State")
            .AddField("Status", "Done!")
            .Build();
        await message.ModifyAsync(embed4);
    }
    
    [Command("setparent")] [Aliases("sp")]
    [Description("Sets the Parent ID")]
    public async Task SetParent(CommandContext ctx, [Description("Parent ID")] string parent)
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

        Configuration.Config.ParentId = parent;
        Configuration.SaveConfiguration();

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Set Parent ID")
            .WithDescription("Parent ID has been changed.")
            .Build();
        await ctx.RespondAsync(embed1);
    }
    
    [Command("setcurrent")] [Aliases("sc")]
    [Description("Sets the Current ID")]
    public async Task SetCurrent(CommandContext ctx, [Description("Current ID")] string current)
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

        Configuration.Config.CurrentId = current;
        Configuration.SaveConfiguration();

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Set Current ID")
            .WithDescription("Current ID has been changed.")
            .Build();
        await ctx.RespondAsync(embed1);
    }
    
    [Command("delete")] [Aliases("vdel", "vd")]
    [Description("Delete Current VM")] 
    public async Task Delete(CommandContext ctx)
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

        if (string.IsNullOrEmpty(Configuration.Config.CurrentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Current VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        if (string.IsNullOrEmpty(Configuration.Config.ParentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Parent VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Reset")
            .AddField("Status", 
                 "Deleting current VM...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        VMware.DeleteVM(false);
        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Reset")
            .AddField("Status", "Done!")
            .AddField("Current ID", 
                Configuration.Config.CurrentId)
            .Build();
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Red)
            .WithTitle("DisControl | Current VM deleted")
            .AddField("Description", "Current VM just got deleted!")
            .AddField("Additional", "Now you are unable to interact with DisControl.")
            .Build());
        await message.ModifyAsync(embed4);
    }
    
    [Command("create")] [Aliases("cr")]
    [Description("Create a new VM")]
    public async Task Create(CommandContext ctx)
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

        if (string.IsNullOrEmpty(Configuration.Config.ParentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Parent VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Reset")
            .AddField("Status", 
                "Creating a new VM...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        VMware.CreateVM();
        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Reset")
            .AddField("Status", "Done!")
            .AddField("Current ID", 
                Configuration.Config.CurrentId)
            .Build();
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | A new VM was just created!")
            .AddField("Description", "A new VM just got created!")
            .AddField("Additional", "Now you are able to interact with DisControl.")
            .Build());
        await message.ModifyAsync(embed4);
    }

    [Command("reset")] [Aliases("rs")]
    [Description("Delete Current VM and create a new one")]
    public async Task Reset(CommandContext ctx)
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

        if (string.IsNullOrEmpty(Configuration.Config.CurrentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Current VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        if (string.IsNullOrEmpty(Configuration.Config.ParentId)) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("Parent VM ID is not set!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Reset")
            .AddField("Status", 
                 "Deleting current VM...")
            .Build();
        var message = await ctx.RespondAsync(embed2);
        VMware.DeleteVM(false);
        var embed3 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Reset")
            .AddField("Status", 
                "Creating a new VM...")
            .Build();
        await message.ModifyAsync(embed3);
        VMware.CreateVM();
        var embed4 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Reset")
            .AddField("Status", "Done!")
            .AddField("Current ID", 
                Configuration.Config.CurrentId)
            .Build();
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Current VM just got reset!")
            .AddField("Description", "The VM was deleted, and a new one was created!")
            .AddField("Additional", "Now you are able to interact with DisControl.")
            .Build());
        await message.ModifyAsync(embed4);
    }
}