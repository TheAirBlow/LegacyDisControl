using Ngrok.AgentAPI;
using Spectre.Console;

namespace DisControl.Bot;

public static class Ngrok
{
    public static NgrokAgentClient Client;
    public static TunnelDetail Tunnel = new();

    private static void Handler(object? sender, EventArgs e)
        => Client.StopTunnel("DisControl VNC");

    public static void StartTunnel()
    {
        Client = new();
        var address = $"{VMWare.GetHostIP()}:5900";
        Tunnel = Client.StartTunnel(new TunnelConfiguration(
            "DisControl", "tcp", 
            $"{address}") {
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