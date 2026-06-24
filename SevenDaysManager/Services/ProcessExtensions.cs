using System.Diagnostics;

namespace SevenDaysManager.Services;

internal static class ProcessExtensions
{
    internal static async Task<bool> WaitForExitAsync(this Process p, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try   { await p.WaitForExitAsync(cts.Token); return true; }
        catch (OperationCanceledException) { return false; }
    }
}
