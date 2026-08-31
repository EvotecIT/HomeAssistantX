namespace HomeAssistantX.WebSockets;

internal sealed class HomeAssistantSubscriptionProjectionException : Exception
{
    internal HomeAssistantSubscriptionProjectionException(Exception failure)
        : base("A Home Assistant subscription payload could not be projected.", failure)
    {
        Failure = failure;
    }

    internal Exception Failure { get; }

    internal static T Capture<T>(Func<T> projection)
    {
        try
        {
            return projection();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new HomeAssistantSubscriptionProjectionException(ex);
        }
    }
}
