using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Spectre.Console;

namespace DisControl.Bot;

// ReSharper disable once InconsistentNaming
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class VMware
{
    private static bool _currentFound;
    private static bool _parentFound;
    public static PowerState State;
    
    /// <summary>
    /// Send an API request
    /// </summary>
    /// <param name="body">Request body</param>
    /// <param name="endpoint">Endpoint URL</param>
    /// <param name="method">HTTP Method</param>
    /// <param name="content">Set Content-Type</param>
    /// <returns></returns>
    public static string SendRequest(string endpoint, string method, bool content = false, string body = "")
    {
        using var client = new BetterWebClient();
        client.Headers.Set("Accept", "application" +
                                     "/vnd.vmware.vmw.rest-v1+json");
        if (content)
            client.Headers.Set("Content-Type", "application" +
                                         "/vnd.vmware.vmw.rest-v1+json");
        client.Headers.Set("Authorization", 
            $"Basic {Configuration.Config.ApiToken}");
        if (string.IsNullOrEmpty(body)) {
            switch (method) {
                case "GET":
                    return client.DownloadString($"{Configuration.Config.ApiUrl}{endpoint}");
                case "POST":
                    return Encoding.UTF8.GetString(client.DownloadData(
                        $"{Configuration.Config.ApiUrl}{endpoint}"));
                default:
                    return Encoding.UTF8.GetString(client.UploadValues(
                        $"{Configuration.Config.ApiUrl}{endpoint}", 
                        method, new NameValueCollection()));
            }
        }

        return client.UploadString(
            $"{Configuration.Config.ApiUrl}{endpoint}",
            method, body);
    }

    private class IpJson
    {
        [JsonProperty("ip")] public string Ip;
    }

    /// <summary>
    /// Get VM's IP
    /// </summary>
    /// <returns>IP Address</returns>
    public static string GetIP()
    {
        var id = Configuration.Config.CurrentId;
        var response = SendRequest($"/vms/{id}/ip", "GET");
        return JsonConvert.DeserializeObject<IpJson>(response)!.Ip;
    }

    /// <summary>
    /// Get Host IP
    /// </summary>
    /// <returns></returns>
    public static string GetHostIP()
    {
        var ip = GetIP();
        var split = ip.Split('.');
        split[^1] = "1";
        return string.Join(".", split);
    }
    
    private class VmIdJson
    {
        [JsonProperty("id")] public string Id;
    }
    
    /// <summary>
    /// Creates a new VM
    /// </summary>
    public static void CreateVM()
    {
        AnsiConsole.MarkupLine($"[yellow]Creating a new VM...[/]");
        var id = Configuration.Config.ParentId;
        var body = "{" + $"\"name\":\"DisControl Instance\", \"parentId\":\"{id}\"" + "}";
        var response = SendRequest("/vms", "POST", true, body);
        Configuration.Config.CurrentId = JsonConvert
            .DeserializeObject<VmIdJson>(response)!.Id;
        Configuration.SaveConfiguration();
        AnsiConsole.MarkupLine($"[green]Created a new VM: {Configuration.Config.CurrentId}[/]");
    }

    /// <summary>
    /// Deleted current VM
    /// </summary>
    public static void DeleteVM(bool parent)
    {
        AnsiConsole.MarkupLine($"[yellow]Deleting current VM...[/]");
        var id = parent ? Configuration.Config.ParentId
            : Configuration.Config.CurrentId;
        SendRequest($"/vms/{id}", "DELETE");
        Configuration.Config.CurrentId = "";
        Configuration.SaveConfiguration();
        AnsiConsole.MarkupLine($"[green]Current VM has been successfully deleted![/]");
    }
    
    public enum PowerState { poweredOn, poweredOff, paused, suspended, resetting }

    private class PowerStateJson
    {
        [JsonProperty("power_state")] public PowerState State;
    }

    /// <summary>
    /// Get current VM's power state
    /// </summary>
    /// <returns>Power State</returns>
    public static PowerState GetState() {
        var id = Configuration.Config.CurrentId;
        var response = SendRequest($"/vms/{id}/power", "GET");
        return JsonConvert.DeserializeObject<PowerStateJson>(response)!.State;
    }

    /// <summary>
    /// Sets the VM's power state
    /// </summary>
    /// <param name="state"></param>
    public static async Task SetState(string state)
    {
        var id = Configuration.Config.CurrentId;
        SendRequest($"/vms/{id}/power", 
            "PUT", true, state);
        var power = "";
        var color = DiscordColor.Aquamarine;
        switch (GetState()) {
            case PowerState.paused:
                color = DiscordColor.Yellow;
                power = "Paused";
                break;
            case PowerState.suspended:
                color = DiscordColor.Yellow;
                power = "Suspended";
                break;
            case PowerState.resetting:
                color = DiscordColor.Yellow;
                power = "Resetting";
                break;
            case PowerState.poweredOff:
                color = DiscordColor.Red;
                power = "Powered OFF";
                break;
            case PowerState.poweredOn:
                color = DiscordColor.Green;
                power = "Powered ON";
                break;
        }
        
        await DiscordLogs.SendEmbed(new DiscordEmbedBuilder()
            .WithColor(color)
            .WithTitle("DisControl | VM's Power State change")
            .AddField("Description", "VM's Power State just changed!")
            .AddField("Power State", power)
            .Build());
    }

    /// <summary>
    /// Check the token and API URL
    /// </summary>
    /// <returns>Boolean value</returns>
    public static void CheckData()
    {
        var content = SendRequest("/vms", "GET");
        var array = JArray.Parse(content).ToObject<object[]>();
        foreach (var i in array!) {
            dynamic obj = i;
            if (obj.id == Configuration.Config.ParentId)
                _parentFound = true;
        }
        
        foreach (var i in array) {
            dynamic obj = i;
            if (obj.id == Configuration.Config.CurrentId)
                _currentFound = true;
        }

        if (!_currentFound) {
            AnsiConsole.MarkupLine("[yellow]Warning: Current VM could not be found.[/]");
            AnsiConsole.MarkupLine("[yellow]Please create a new one with a command.[/]");
            Configuration.Config.CurrentId = "";
            Configuration.SaveConfiguration();
        }
        
        if (!_parentFound) {
            AnsiConsole.MarkupLine("[yellow]Warning: Parent VM could not be found.[/]");
            AnsiConsole.MarkupLine("[yellow]Please set a new one with a command.[/]");
            Configuration.Config.ParentId = "";
            Configuration.SaveConfiguration();
        }
    }

    private static async void StateThread()
    {
        while (true) {
            state = GetState();
            await Task.Delay(1000);
        }
    }

    public static void Initialize()
    {
        AnsiConsole.MarkupLine("[cyan]Task: Verifying VMWare API data...[/]");
        try { CheckData(); } 
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
        
        new Thread(StateThread).Start();
    }
}