using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace DisControl.Bot.Attributes;

/// <summary>
/// Check if the current VM is enabled
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class VMPoweredOnAttribute : CheckBaseAttribute
{
    public override Task<bool> ExecuteCheckAsync(CommandContext ctx, bool help)
        => Task.FromResult(VMWare.GetState() == VMWare.PowerState.poweredOn);
}