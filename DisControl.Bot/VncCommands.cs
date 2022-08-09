using System.Collections.Immutable;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Rendering;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;
using Spectre.Console;
using Size = MarcusW.VncClient.Size;

namespace DisControl.Bot;

public class VncCommands : BaseCommandModule
{
    private sealed class FramebufferReference : IFramebufferReference
    {
        private volatile SKBitmap? _bitmap;
        
        public IntPtr Address => _bitmap?.GetPixels() ?? throw new ObjectDisposedException(nameof(FramebufferReference));
        public Size Size => new Size(_bitmap?.Width ?? throw new ObjectDisposedException(nameof(FramebufferReference)), 
            _bitmap?.Height ?? throw new ObjectDisposedException(nameof(FramebufferReference)));
        public PixelFormat Format => Conversions.GetPixelFormat(_bitmap?.ColorType ?? throw new ObjectDisposedException(nameof(FramebufferReference)));
        public double HorizontalDpi => 1;
        public double VerticalDpi => 1;

        internal FramebufferReference(SKBitmap bitmap)
        {
            _bitmap = bitmap;
        }
        
        public void Dispose()
            => _bitmap?.Dispose();
    }
    
    private class RenderTarget : IRenderTarget
    {
        public volatile SKBitmap? _bitmap;
        private readonly object _bitmapReplacementLock = new();

        public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
        {
            bool sizeChanged = _bitmap == null || _bitmap.Width != size.Width || _bitmap.Height != size.Height;

            SKBitmap bitmap;
            if (sizeChanged) {
                bitmap = new SKBitmap(size.Width, size.Height);
                
                lock (_bitmapReplacementLock) {
                    _bitmap?.Dispose();
                    _bitmap = bitmap;
                }
            }

            return new FramebufferReference(_bitmap!);
        }
    }
    
    private VncClient _client = new(new NullLoggerFactory());
    private RfbConnection? _connection;
    private RenderTarget _target = new();
    private volatile bool _screen = false;
    
    private async Task<(DiscordMessage, bool)> Connect(CommandContext ctx)
    {
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Connect to VNC")
            .AddField("Status", "Connecting...")
            .Build();
        var message = await ctx.RespondAsync(embed1);

        try { 
            _connection = await _client.ConnectAsync(new ConnectParameters {
                ConnectTimeout = TimeSpan.FromSeconds(5),
                MaxReconnectAttempts = 5, 
                TransportParameters = new TcpTransportParameters {
                    Host = VMware.GetHostIP(),
                    Port = 5901
                }});
            _connection.RenderTarget = _target;
        }
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

    [Command("screen")]
    [Description("Get a screenshot")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Screen(CommandContext ctx)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (_screen)
            return;
        
        _screen = true;
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Saving file...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed2);
        else msg = await ctx.RespondAsync(embed2);
        if (File.Exists("image.jpg"))
            File.Delete("image.jpg");
        var stream = new FileStream("image.jpg", 
            FileMode.CreateNew, FileAccess.ReadWrite);
        using var wstream = new SKManagedWStream(stream);
        _target._bitmap?.Encode(wstream, SKEncodedImageFormat.Png, 24);
        var embed3 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Uploading...")
            .Build();
        await msg.ModifyAsync(embed3);
        var file = new DiscordMessageBuilder()
            .WithFile(stream);
        await stream.DisposeAsync();
        await msg.ModifyAsync(file);
        await msg.ModifyEmbedSuppressionAsync(true);
        _screen = false;
    }

    [Command("keycombo")]
    [Description("Input a keycombo")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task KeyCombo(CommandContext ctx, [Description("Keys to press")] params string[] keys)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

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
                    return KeySymbol.ISO_Enter;
                return x.Length > 1 ? Enum.Parse<KeySymbol>(x) 
                    : DataSet.FromUnicode(x[0]);
            });
            var keySyms = enumKeys as KeySymbol[] ?? enumKeys.ToArray();
            foreach (var i in keySyms)
                _connection?.SendMessageAsync(new KeyEventMessage(true, i));
            foreach (var i in keySyms)
                _connection?.SendMessageAsync(new KeyEventMessage(false, i));
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
    
    [Command("enter")]
    [Description("Press NumPad Enter key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Enter(CommandContext ctx)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Enter")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new KeyEventMessage(true, KeySymbol.ISO_Enter));
        _connection?.SendMessageAsync(new KeyEventMessage(false, KeySymbol.ISO_Enter));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Enter")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("enter")]
    [Description("Press NumPad Enter key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Backspace(CommandContext ctx, [Description("How many times")] int count)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Backspace")
            .AddField("Status", "Sending keys...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);

        for (var i = 0; i < count; i++) {
            _connection?.SendMessageAsync(new KeyEventMessage(true, KeySymbol.BackSpace));
            _connection?.SendMessageAsync(new KeyEventMessage(false, KeySymbol.BackSpace));
        }

        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Backspace")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("print")]
    [Description("Print some text")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Print(CommandContext ctx, [RemainingText] [Description("Text to print")] string text)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

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
            var keySyms = enumKeys as KeySymbol[] ?? enumKeys.ToArray();
            foreach (var i in keySyms) {
                _connection?.SendMessageAsync(new KeyEventMessage(true, i));
                _connection?.SendMessageAsync(new KeyEventMessage(false, i));
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
    
    [Command("mouse")]
    [Description("Move mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task Mouse(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Move Mouse")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.None));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Move Mouse")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }

    [Command("leftclick")]
    [Description("Left mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task LeftClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Left Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.Left));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Left Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("rightclick")]
    [Description("Right mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task RightClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Right Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.Right));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Right Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("middleclick")]
    [Description("Middle mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task MiddleClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Middle Click")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.Middle));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Middle Click")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("scrollup")]
    [Description("Scroll up mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollUp(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Scroll Up")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.WheelUp));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Scroll Up")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
    
    [Command("scrolldown")]
    [Description("Scroll Down mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollDown(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
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

        if (VMware.GetState() != VMware.PowerState.poweredOn) {
            var error = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Yellow)
                .WithTitle("DisControl | Error")
                .WithDescription("VM is not powered on!")
                .Build();
            await ctx.RespondAsync(error);
            return;
        }
        
        DiscordMessage msg = null!;
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }
        
        var embed1 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Scroll Down")
            .AddField("Status", "Sending...")
            .Build();
        if (msg != null!) msg = await msg.ModifyAsync(embed1);
        else msg = await ctx.RespondAsync(embed1);
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), MouseButtons.WheelDown));
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Green)
            .WithTitle("DisControl | Scroll Down")
            .AddField("Status", "Done!")
            .Build();
        await msg.ModifyAsync(embed2);
    }
}