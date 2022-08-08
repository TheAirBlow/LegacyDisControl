using DSharpPlus.Entities;
using Spectre.Console;

namespace DisControl.Bot;

public static class AutoRestart
{
    private static volatile bool _stop;
    private static Thread _thread;

    public static void Start()
    {
        if (_thread is { IsAlive: true })
            throw new Exception(
                "Thread is still alive!");
        _stop = false;
        _thread = new(Thread);
        _thread.Start();
    }

    public static void Stop()
        => _stop = true;

    private static async void Thread()
    {
        while (true) {
            if (_stop || string.IsNullOrEmpty(Configuration.Config.CurrentId)) return;
            await Task.Delay(5000);
            try { 
                var state = VMWare.GetState();
                if (state == VMWare.PowerState.poweredOff)
                    await VMWare.SetState("on");
                await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
                    .WithColor(DiscordColor.Green)
                    .WithTitle("DisControl | Auto Restart")
                    .WithDescription("The VM is off, powering it on...")
                    .Build());
            } catch (Exception e) {
                AnsiConsole.MarkupLine("[red]Unable to set machine's state![/]");
                AnsiConsole.WriteException(e);
            }
        }
    }
}