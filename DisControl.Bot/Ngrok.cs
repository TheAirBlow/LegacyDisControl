using Ngrok.AgentAPI;
using Spectre.Console;

namespace DisControl.Bot;

public static class Ngrok
{
    public static NgrokAgentClient Client;
    public static TunnelDetail Tunnel = new();

    private static void Handler(object? sender, EventArgs e)
        => Client.StopTunnel("DisControl");

    public static void StartTunnel()
    {
        Client = new();
        var address = $"{VMware.GetHostIP()}:5901";
        AnsiConsole.WriteLine(address);
        Tunnel = Client.StartTunnel(new TunnelConfiguration(
            "DisControl", "tcp", address) {
            HostHeader = address
        });
        AppDomain.CurrentDomain.ProcessExit += Handler;
        Console.CancelKeyPress += Handler;
    }

    public static void StopTunnel()
    {
        Handler(null, null!); Tunnel = new();
        AppDomain.CurrentDomain.ProcessExit -= Handler;
        Console.CancelKeyPress -= Handler;
    }
}