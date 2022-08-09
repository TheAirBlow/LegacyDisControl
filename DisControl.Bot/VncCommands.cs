using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using MarcusW.VncClient.Protocol.Implementation.Services.Transports;
using MarcusW.VncClient.Protocol.SecurityTypes;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Security;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Spectre.Console;
using Size = MarcusW.VncClient.Size;

namespace DisControl.Bot;

public class VncCommands : BaseCommandModule
{
    private class DemoAuthenticationHandler : IAuthenticationHandler
    {
        public async Task<TInput> ProvideAuthenticationInputAsync<TInput>(RfbConnection connection, ISecurityType securityType, IAuthenticationInputRequest<TInput> request)
            where TInput : class, IAuthenticationInput {
            if (typeof(TInput) == typeof(PasswordAuthenticationInput))
                throw new InvalidOperationException("The authentication input request is not supported by this authentication handler.");
            
            return (TInput)Convert.ChangeType(new PasswordAuthenticationInput(""), typeof(TInput));
        }
    }
    
    private sealed class FramebufferReference : IFramebufferReference, IDisposable
    {
        private IntPtr _address;
        private Size _size;
        
        public IntPtr Address => _address;
        public Size Size => _size;
        public PixelFormat Format => PixelFormat.Plain;
        public double HorizontalDpi => _size.Width;
        public double VerticalDpi => _size.Height;

        internal unsafe FramebufferReference(byte[] bitmap, Size size)
        {
            _address = (IntPtr)bitmap.AsMemory().Pin().Pointer;
            _size = size;
        }

        public void Dispose() { }
    }
    
    private class RenderTarget : IRenderTarget
    {
        private readonly object _bitmapReplacementLock = new();
        public volatile byte[] Bitmap;
        public Size Size;

        public IFramebufferReference GrabFramebufferReference(Size size, IImmutableSet<Screen> layout)
        {
            byte[]? bitmap;
            if (Size == null) Size = size;
            if (Bitmap == null || Size != size) {
                bitmap = new byte[size.Width * size.Height * Unsafe.SizeOf<Argb32>()];
                lock (_bitmapReplacementLock)
                    Bitmap = bitmap;

                Size = size;
            }

            return new FramebufferReference(Bitmap, size);
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
                AuthenticationHandler = new DemoAuthenticationHandler(),
                TransportParameters = new TcpTransportParameters {
                    Host = VMware.GetHostIP(),
                    Port = 5901
                }});
            _connection.RenderTarget = _target;
        }
        catch (Exception e) {
            await message.ModifyAsync($"```csharp\n{e}```");
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
    
    public async Task ScreenAction(CommandContext ctx, DiscordMessage msg = null!)
    {
        if (_connection == null || _connection.ConnectionState != ConnectionState.Connected) {
            (msg, var error) = await Connect(ctx);
            if (error) return;
        }

        if (_screen) return;
        
        _screen = true;
        var embed2 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Converting into PNG...")
            .Build();
        if (msg != null!) msg = await msg!.ModifyAsync(embed2);
        else msg = await ctx.RespondAsync(embed2);
        var stream = new MemoryStream();
        using (var image = Image.LoadPixelData<Argb32>(_target.Bitmap,
                   _target.Size.Width, _target.Size.Height))
            await image.SaveAsPngAsync(stream);
        var embed3 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Uploading...")
            .Build();
        await msg.ModifyAsync(embed3);
        var file = new DiscordMessageBuilder()
            .WithFile("screenshot.png", stream, true);
        await msg.ModifyAsync(file);
        await msg.ModifyEmbedSuppressionAsync(true);
        await stream.DisposeAsync();
        _screen = false;
    }

    [Command("screenshot")] [Aliases("screen", "s")]
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

        await ScreenAction(ctx);
    }
    
    public async Task KeyComboAction(CommandContext ctx, [Description("Keys to press")] params string[] keys)
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
                .AddField("Additional", "[Click here](https://bit.ly/3dji1Zx)")
                .Build();
            await msg.ModifyAsync(embed);
            failed = true;
        }

        if (!failed) await ScreenAction(ctx, msg);
    }

    [Command("keycombo")] [Aliases("key", "k")]
    [Description("Input a keycombo")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task KeyCombo(CommandContext ctx, [Description("Keys to press")] params string[] keys)
        => await KeyComboAction(ctx, keys);
    
    [Command("selectall")] [Aliases("all")]
    [Description("Select all text (Ctrl + A)")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task SelectAll(CommandContext ctx)
        => await KeyComboAction(ctx, "Control_L A");
    
    [Command("clear")] [Aliases("c")]
    [Description("Select all text (Ctrl + A) and remove it (Backspace)")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task RemoveAllText(CommandContext ctx)
        => await KeyComboAction(ctx, "Control_L A BackSpace");
    
    [Command("copy")] [Aliases("cp")]
    [Description("Copy selected text (Ctrl + C)")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Copy(CommandContext ctx)
        => await KeyComboAction(ctx, "Control_L C");
    
    [Command("cut")] [Aliases("ct")]
    [Description("Cut selected text (Ctrl + X)")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Cut(CommandContext ctx)
        => await KeyComboAction(ctx, "Control_L X");
    
    [Command("paste")] [Aliases("p")]
    [Description("Cut selected text (Ctrl + X)")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Paste(CommandContext ctx)
        => await KeyComboAction(ctx, "Control_L V");
    
    private async Task KeyboardAction(CommandContext ctx, KeySymbol key)
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
        _connection?.SendMessageAsync(new KeyEventMessage(true, key));
        _connection?.SendMessageAsync(new KeyEventMessage(false, key));
        await ScreenAction(ctx, msg);
    }

    [Command("enter")] [Aliases("e")]
    [Description("Press Enter key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Enter(CommandContext ctx)
        => await KeyboardAction(ctx, KeySymbol.KP_Enter);
    
    [Command("win")] [Aliases("w")]
    [Description("Press Win key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Win(CommandContext ctx)
        => await KeyboardAction(ctx, KeySymbol.Super_L);

    [Command("backspace")] [Aliases("rm", "del", "back")]
    [Description("Press Backspace key on the VM")]
    [Cooldown(1, 5, CooldownBucketType.Global)]
    public async Task Backspace(CommandContext ctx, [Description("How many times")] int count)
        => await KeyboardAction(ctx, KeySymbol.BackSpace);
    
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

        var enumKeys = text.ToArray().Select(DataSet.FromUnicode);
        var keySyms = enumKeys as KeySymbol[] ?? enumKeys.ToArray();
        foreach (var i in keySyms) {
            _connection?.SendMessageAsync(new KeyEventMessage(true, i));
            _connection?.SendMessageAsync(new KeyEventMessage(false, i));
        }
        
        if (!failed) await ScreenAction(ctx, msg);
    }
    
    public async Task MouseAction(CommandContext ctx, int x, int y, MouseButtons button)
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
        _connection?.SendMessageAsync(new PointerEventMessage(new Position(x, y), button));
        await ScreenAction(ctx, msg);
    }

    [Command("mouse")]
    [Description("Move mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task Mouse(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.None);

    [Command("leftclick")]
    [Description("Left mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task LeftClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.Left);
    
    [Command("rightclick")]
    [Description("Right mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task RightClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.Right);
    
    [Command("middleclick")]
    [Description("Middle mouse click")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task MiddleClick(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.Middle);
    
    [Command("scrollup")]
    [Description("Scroll up mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollUp(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.WheelUp);
    
    [Command("scrolldown")]
    [Description("Scroll Down mouse")]
    [Cooldown(1, 2, CooldownBucketType.Global)]
    public async Task ScrollDown(CommandContext ctx, [Description("Mouse X position")] int x, [Description("Mouse Y position")] int y)
        => await MouseAction(ctx, x, y, MouseButtons.WheelDown);
}