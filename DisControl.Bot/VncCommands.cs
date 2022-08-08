using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using DisControl.Bot.Attributes;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using RemoteViewing.Vnc;
using RemoteViewing.Windows.Forms;
using Spectre.Console;

namespace DisControl.Bot;

public class VncCommands : BaseCommandModule
{
    private VncClient _client = new();
    private volatile bool _screen = false;
    
    private async Task<(DiscordMessage, bool)> Connect(CommandContext ctx)
    {
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Connect to VNC")
            .AddField("Status", "Connecting...")
            .Build();
        var message = await ctx.RespondAsync(embed1);

        try { _client.Connect(VMWare.GetHostIP()); }
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Unable to connect to VNC![/]");
            AnsiConsole.WriteException(e);
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", "VNC Connection failed: An exception occured!")
                .AddField("Additional", "See logs for more information.")
                .Build();
            await message.ModifyAsync(embed);
            return (message, true);
        }
        
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Connect to VNC")
            .AddField("Status", "Connected!")
            .Build();
        await message.ModifyAsync(embed2);
        return (message, false);
    }

    [Command("screen")] [CurrentIdSet]
    [Description("Get a screenshot")] [VMPoweredOn]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Screen(CommandContext ctx)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected || _screen)
            return;
        
        _screen = true;
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Getting framebuffer...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);

        var w = _client.Framebuffer.Width;
        var h = _client.Framebuffer.Height;
        var bitmap = new Bitmap(w, h, PixelFormat.Format32bppRgb);
        VncBitmap.CopyFromFramebuffer(_client.Framebuffer, 
            new VncRectangle(0, 0, w, h),
            bitmap, 0, 0);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Saving file...")
            .Build();
        await msg.ModifyAsync(embed2);
        if (File.Exists("image.jpg"))
            File.Delete("image.jpg");
        bitmap.Save("image.jpg");
        var embed3 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Uploading...")
            .Build();
        await msg.ModifyAsync(embed3);
        await using var stream = new FileStream(
            "image.jpg", FileMode.Open);
        var file = new DiscordMessageBuilder()
            .WithFile(stream);
        await msg.ModifyAsync(file);
        await msg.ModifyEmbedSuppressionAsync(true);
        _screen = false;
    }

    [Command("keycombo")] [VMPoweredOn]
    [Description("Input a keycombo")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task KeyCombo(CommandContext ctx, [Description("Keys to press")] params string[] keys)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Key Combo")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        var failed = false;

        try {
            var enumKeys = keys.Select(x => {
                if (x.ToLower() == "enter" || x.ToLower() == "numpadenter") 
                    return KeySym.NumPadEnter;
                return x.Length > 1 ? Enum.Parse<KeySym>(x) 
                    : DataSet.FromUnicode(x[0]);
            });
            var keySyms = enumKeys as KeySym[] ?? enumKeys.ToArray();
            foreach (var i in keySyms)
                _client.SendKeyEvent(i, true);
            foreach (var i in keySyms)
                _client.SendKeyEvent(i, false);
        } catch (Exception e) {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", $"Unable to parse {e.Message}!")
                .AddField("Additional", "[Click here](https://bit.ly/3MnPT4u)")
                .Build();
            await msg.ModifyAsync(embed);
            failed = true;
        }

        if (!failed) {
            var embed2 = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Green)
                .WithTitle("DisControl | Print")
                .AddField("Status", "Done!")
                .Build();
            await msg.ModifyAsync(embed2);
        }
    }
    
    [Command("enter")] [VMPoweredOn]
    [Description("Press NumPad Enter key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Enter(CommandContext ctx)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Enter")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendKeyEvent(KeySym.NumPadEnter, true);
        _client.SendKeyEvent(KeySym.NumPadEnter, false);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Enter")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("enter")] [VMPoweredOn]
    [Description("Press NumPad Enter key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Backspace(CommandContext ctx, [Description("How many times")] int count)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Backspace")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);

        for (var i = 0; i < count; i++) {
            _client.SendKeyEvent(KeySym.Backspace, true);
            _client.SendKeyEvent(KeySym.Backspace, false);
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Backspace")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("print")] [VMPoweredOn]
    [Description("Print some text")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Print(CommandContext ctx, [RemainingText] [Description("Text to print")] string text)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Print")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        var failed = false;

        try {
            var enumKeys = text.ToArray().Select(DataSet.FromUnicode);
            var keySyms = enumKeys as KeySym[] ?? enumKeys.ToArray();
            foreach (var i in keySyms) {
                _client.SendKeyEvent(i, true);
                _client.SendKeyEvent(i, false);
            }
        } catch (Exception e) {
            var embed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("DisControl | Error")
                .AddField("Message", $"Unable to parse {e.Message}!")
                .AddField("Additional", "[Click here](https://bit.ly/3MnPT4u)")
                .Build();
            await msg.ModifyAsync(embed);
            failed = true;
        }
        
        if (!failed) {
            var embed2 = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Green)
                .WithTitle("DisControl | Print")
                .AddField("Status", "Done!")
                .Build();
            await msg.ModifyAsync(embed2);
        }
    }
    
    [Command("mouse")] [VMPoweredOn]
    [Description("Move mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task Mouse(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Move Mouse")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 0);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Move Mouse")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }

    [Command("leftclick")] [VMPoweredOn]
    [Description("Left mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task LeftClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Left Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 1);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Left Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("rightclick")] [VMPoweredOn]
    [Description("Right mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task RightClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Right Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 4);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Right Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("middleclick")] [VMPoweredOn]
    [Description("Middle mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task MiddleClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Middle Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 2);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Middle Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("scrollup")] [VMPoweredOn]
    [Description("Scroll up mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollUp(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Scroll Up")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 8);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Scroll Up")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("scrolldown")] [VMPoweredOn]
    [Description("Scroll Down mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollDown(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
    {
        DiscordMessage msg = null!;
        if (!_client.IsConnected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (!_client.IsConnected)
            return;
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Scroll Down")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _client.SendPointerEvent(x, y, 16);
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Scroll Down")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
}