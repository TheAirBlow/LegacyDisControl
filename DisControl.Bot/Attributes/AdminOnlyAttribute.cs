using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace DisControl.Bot.Attributes;

/// <summary>
/// Check is the user an admin
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class AdminOnlyAttribute : CheckBaseAttribute
{
    public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        => Task.FromResult(Configuration.Config.AdminId.Contains(ctx.Member.Id));
}