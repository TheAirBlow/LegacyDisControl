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
        private volatile byte[]? _bitmap;
        private IntPtr _address;
        private Size _size;
        
        public IntPtr Address => _address;
        public Size Size => _size;
        public PixelFormat Format => PixelFormat.Plain;
        public double HorizontalDpi => _size.Width;
        public double VerticalDpi => _size.Height;

        internal FramebufferReference(byte[] bitmap, Size size)
        {
            _address = GCHandle.Alloc(_bitmap, GCHandleType.Pinned).AddrOfPinnedObject();
            _bitmap = bitmap; _size = size;
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
            if (Bitmap == null || Size.Width != size.Width || Size.Height != size.Height) {
                bitmap = new byte[size.Width * size.Height * Unsafe.SizeOf<Rgba32>()];
                lock (_bitmapReplacementLock)
                    Bitmap = bitmap;

                Size = size;
            }

            return new FramebufferReference(Bitmap!, size);
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
        if (File.Exists("image.png"))
            File.Delete("image.png");
        using (var image = Image.LoadPixelData<Rgba32>(_target.Bitmap, _target.Size.Width, _target.Size.Height))
            await image.SaveAsPngAsync("image.png");
        var embed3 = new DiscordEmbedBuilder()
            .WithColor(DiscordColor.Yellow)
            .WithTitle("DisControl | Screenshot")
            .AddField("Status", "Uploading...")
            .Build();
        await msg.ModifyAsync(embed3);
        var stream = new FileStream("image.png", 
            FileMode.Open, FileAccess.ReadWrite);
        var file = new DiscordMessageBuilder()
            .WithFile("screenshot.png", stream);
        await msg.ModifyAsync(file);
        await msg.ModifyEmbedSuppressionAsync(true);
        await stream.DisposeAsync();
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