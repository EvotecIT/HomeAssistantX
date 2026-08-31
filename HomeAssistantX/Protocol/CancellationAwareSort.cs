namespace HomeAssistantX.Protocol;

internal static class CancellationAwareSort
{
    internal static void Sort<T>(List<T> values, Comparison<T> comparison)
    {
        try
        {
            values.Sort(comparison);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is OperationCanceledException cancellation)
        {
            throw cancellation;
        }
    }
}
