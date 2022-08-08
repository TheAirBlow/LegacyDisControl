using Newtonsoft.Json;
using Spectre.Console;

namespace DisControl.Bot;

public static class Configuration
{
    public class ConfigJson
    {
        /// <summary>
        /// Discord Bot token
        /// </summary>
        public string BotToken;

        /// <summary>
        /// Guild ID
        /// </summary>
        public ulong GuildId;
        
        /// <summary>
        /// Channel ID for Logs
        /// </summary>
        public ulong LogsChannelId;
        
        /// <summary>
        /// Channel ID for Commands
        /// </summary>
        public ulong CommandsChannelId;
        
        /// <summary>
        /// VMWare API token
        /// </summary>
        public string ApiToken;

        /// <summary>
        /// VMWare API URL
        /// </summary>
        public string ApiUrl;
        
        /// <summary>
        /// VMWare Original VM ID
        /// </summary>
        public string ParentId;
        
        /// <summary>
        /// VMWare Current VM ID
        /// </summary>
        public string CurrentId;

        /// <summary>
        /// Discord Bot Prefix
        /// </summary>
        public string BotPrefix;

        /// <summary>
        /// Admin's Discord User ID
        /// </summary>
        public List<ulong> AdminId;
    }

    public static ConfigJson Config = new();
    
    public static void LoadConfiguration()
    {
        AnsiConsole.MarkupLine("[cyan]Task: Loading configuration file...[/]");
        
        if (!File.Exists("config.json")) {
            AnsiConsole.MarkupLine("[red]Task Failed: The file does not exist.[/]");
            AnsiConsole.MarkupLine("[yellow]The file has been created.[/]");
            AnsiConsole.MarkupLine("[yellow]Please fill it up.[/]");
            SaveConfiguration(); Console.ReadKey(); Environment.Exit(0);
        }
        
        try { Config = JsonConvert.DeserializeObject<ConfigJson>(File.ReadAllText("config.json"))!; }
        catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
        
        AnsiConsole.MarkupLine("[green]Task has successfully finished![/]");
    }

    public static void SaveConfiguration()
        => File.WriteAllText("config.json", JsonConvert
            .SerializeObject(Config, Formatting.Indented));
}