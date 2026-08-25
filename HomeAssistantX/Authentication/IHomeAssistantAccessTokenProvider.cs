namespace HomeAssistantX.Authentication;

/// <summary>Supplies an access token without making token storage a responsibility of HomeAssistantX.</summary>
public interface IHomeAssistantAccessTokenProvider
{
    /// <summary>Returns the access token for the next authenticated request.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
