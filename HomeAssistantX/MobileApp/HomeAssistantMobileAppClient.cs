using System.Net.Http;
using HomeAssistantX.Configuration;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;

namespace HomeAssistantX.MobileApp;

/// <summary>Registers a companion application through Home Assistant's authenticated mobile-app API.</summary>
public sealed class HomeAssistantMobileAppClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantClientOptions _options;

    internal HomeAssistantMobileAppClient(HomeAssistantRestClient rest, HomeAssistantClientOptions options)
    {
        _rest = rest;
        _options = options;
    }

    public async Task<HomeAssistantMobileAppRegistration> RegisterAsync(HomeAssistantMobileAppRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        cancellationToken.ThrowIfCancellationRequested();
        request.Validate();
        var frozenRequest = new HomeAssistantMobileAppRegistrationRequest
        {
            AppId = request.AppId,
            AppName = request.AppName,
            AppVersion = request.AppVersion,
            DeviceName = request.DeviceName,
            Manufacturer = request.Manufacturer,
            Model = request.Model,
            DeviceId = request.DeviceId,
            OperatingSystemName = request.OperatingSystemName,
            OperatingSystemVersion = request.OperatingSystemVersion,
            SupportsEncryption = request.SupportsEncryption,
            AppData = HomeAssistantJson.FreezeObject(request.AppData, nameof(request.AppData), "AppData", cancellationToken)!
        };
        var registration = await _rest.SendAsync<HomeAssistantMobileAppRegistration>(HttpMethod.Post, "api/mobile_app/registrations", frozenRequest, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(registration.WebhookId))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a mobile-app registration without a webhook identifier.");
        }

        if (frozenRequest.SupportsEncryption && string.IsNullOrWhiteSpace(registration.Secret))
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return the requested mobile-app encryption secret.");
        }

        if (!frozenRequest.SupportsEncryption && registration.Secret is not null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a mobile-app encryption secret for a registration that did not request encryption.");
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
        _options.Validate();
        var uri = registration.CloudhookUri ?? new Uri(_options.BaseUri, "api/webhook/" + Uri.EscapeDataString(registration.WebhookId));
        return new HomeAssistantMobileAppWebhookClient(
            uri,
            registration.Secret,
            protector,
            _options.RequestTimeout,
            _options.MaximumRestResponseBytes);
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
