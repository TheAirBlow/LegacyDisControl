using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace DisControl.Bot.Attributes;

/// <summary>
/// Check if Current ID is set
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class CurrentIdSetAttribute : CheckBaseAttribute
{
    public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        => Task.FromResult(!string.IsNullOrEmpty(Configuration.Config.CurrentId));
}