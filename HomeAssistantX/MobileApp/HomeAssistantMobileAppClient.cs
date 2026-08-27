using System.Net.Http;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;

namespace HomeAssistantX.MobileApp;

/// <summary>Registers a companion application through Home Assistant's authenticated mobile-app API.</summary>
public sealed class HomeAssistantMobileAppClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly Uri _baseUri;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maximumResponseBytes;

    internal HomeAssistantMobileAppClient(HomeAssistantRestClient rest, Uri baseUri, TimeSpan requestTimeout, int maximumResponseBytes)
    {
        _rest = rest;
        _baseUri = baseUri;
        _requestTimeout = requestTimeout;
        _maximumResponseBytes = maximumResponseBytes;
    }

    public async Task<HomeAssistantMobileAppRegistration> RegisterAsync(HomeAssistantMobileAppRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        request.Validate();
        var registration = await _rest.SendAsync<HomeAssistantMobileAppRegistration>(HttpMethod.Post, "api/mobile_app/registrations", request, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(registration.WebhookId))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a mobile-app registration without a webhook identifier.");
        }

        if (request.SupportsEncryption && string.IsNullOrWhiteSpace(registration.Secret))
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return the requested mobile-app encryption secret.");
        }

        RequireHttpUri(registration.CloudhookUri, "cloudhook URL");
        RequireHttpUri(registration.RemoteUiUri, "remote UI URL");

        return registration;
    }

    /// <summary>Creates an unauthenticated webhook client with an isolated, credential-free HTTP transport.</summary>
    public HomeAssistantMobileAppWebhookClient CreateWebhookClient(HomeAssistantMobileAppRegistration registration, IHomeAssistantMobileAppPayloadProtector? protector = null)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        if (string.IsNullOrWhiteSpace(registration.WebhookId)) throw new ArgumentException("A webhook identifier is required.", nameof(registration));
        var uri = registration.CloudhookUri ?? new Uri(_baseUri, "api/webhook/" + Uri.EscapeDataString(registration.WebhookId));
        return new HomeAssistantMobileAppWebhookClient(uri, registration.Secret, protector, _requestTimeout, _maximumResponseBytes);
    }

    private static void RequireHttpUri(Uri? value, string name)
    {
        if (value is null) return;
        if (!value.IsAbsoluteUri
            || (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(value.UserInfo))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an invalid mobile-app " + name + ".");
        }
    }
}
