using System.Text;
using MarcusW.VncClient;
using Spectre.Console;

namespace DisControl.Bot;

public static class DataSet
{
    private static Dictionary<long, long> _conversionList = new();

    public static void Initialize()
    {
        AnsiConsole.MarkupLine("[cyan]Task: Loading KeySym <-> Unicode convertion table...[/]");
        try {
            var split = File.ReadAllText("dataset.txt").Split('\n');
            foreach (var i in split) {
                var split2 = i.Split("   ");
                try {
                    _conversionList.Add(Convert.ToInt32(split2[1], 16),
                        Convert.ToInt32(split2[0], 16));
                } catch { /* Ignore */ }
            }
        } catch (Exception e) {
            AnsiConsole.MarkupLine("[red]Task Failed: An exception occured.[/]");
            AnsiConsole.WriteException(e); Console.ReadKey(); Environment.Exit(0);
        }
        AnsiConsole.MarkupLine("[green]Task successfully finished![/]");
    }

    public static KeySymbol FromUnicode(char input)
    {
        var bytes = Encoding.UTF8.GetBytes(input.ToString());
        bytes = AddEmptyBytes(bytes); var str = BitConverter.ToString(bytes);
        str = str.Replace("-", "");
        return (KeySymbol)_conversionList[Convert.ToInt32(str, 16)];
    }

    private static byte[] AddEmptyBytes(byte[] input)
        => input.Length != 2 ? new byte[2] {0x00, input[0]} : input;
}