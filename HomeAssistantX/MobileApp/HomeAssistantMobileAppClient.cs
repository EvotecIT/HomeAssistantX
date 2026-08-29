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
        request.Validate(cancellationToken);
        var frozenAdditionalData = HomeAssistantJson.FreezeObject(
            request.AdditionalData,
            nameof(request.AdditionalData),
            "Additional registration data",
            cancellationToken)!;
        var frozenAdditionalFields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in frozenAdditionalData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var index = 0; index < pair.Key.Length; index++)
            {
                if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            }
            frozenAdditionalFields.Add(pair.Key, pair.Value);
        }
        cancellationToken.ThrowIfCancellationRequested();
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
            AppData = HomeAssistantJson.FreezeObject(request.AppData, nameof(request.AppData), "AppData", cancellationToken)!,
            AdditionalData = frozenAdditionalFields
        };
        var registration = await _rest.SendAsync<HomeAssistantMobileAppRegistration>(HttpMethod.Post, "api/mobile_app/registrations", frozenRequest, cancellationToken).ConfigureAwait(false);
        if (IsNullOrWhiteSpace(registration.WebhookId, cancellationToken))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a mobile-app registration without a webhook identifier.");
        }

        if (frozenRequest.SupportsEncryption && IsNullOrWhiteSpace(registration.Secret, cancellationToken))
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return the requested mobile-app encryption secret.");
        }

        if (!frozenRequest.SupportsEncryption && registration.Secret is not null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a mobile-app encryption secret for a registration that did not request encryption.");
        }

        RequireHttpUri(registration.CloudhookUri, "cloudhook URL", cancellationToken);
        RequireHttpUri(registration.RemoteUiUri, "remote UI URL", cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return registration;
    }

    /// <summary>Creates an unauthenticated webhook client with an isolated, credential-free HTTP transport.</summary>
    public HomeAssistantMobileAppWebhookClient CreateWebhookClient(HomeAssistantMobileAppRegistration registration, IHomeAssistantMobileAppPayloadProtector? protector = null)
        => CreateWebhookClientCore(registration, protector, handlerFactory: null);

    /// <summary>
    /// Creates a webhook client with a caller-supplied, per-client HTTP handler. The factory must
    /// return a credential-free handler that does not follow redirects; the client owns and disposes it.
    /// </summary>
    public HomeAssistantMobileAppWebhookClient CreateWebhookClient(
        HomeAssistantMobileAppRegistration registration,
        IHomeAssistantMobileAppPayloadProtector? protector,
        Func<HttpMessageHandler> handlerFactory)
    {
        if (handlerFactory is null) throw new ArgumentNullException(nameof(handlerFactory));
        return CreateWebhookClientCore(registration, protector, handlerFactory);
    }

    private HomeAssistantMobileAppWebhookClient CreateWebhookClientCore(
        HomeAssistantMobileAppRegistration registration,
        IHomeAssistantMobileAppPayloadProtector? protector,
        Func<HttpMessageHandler>? handlerFactory)
    {
        if (registration is null) throw new ArgumentNullException(nameof(registration));
        if (string.IsNullOrWhiteSpace(registration.WebhookId)) throw new ArgumentException("A webhook identifier is required.", nameof(registration));
        _options.Validate();
        var uri = registration.CloudhookUri ?? new Uri(_options.BaseUri, "api/webhook/" + Uri.EscapeDataString(registration.WebhookId));
        if (handlerFactory is null)
        {
            return new HomeAssistantMobileAppWebhookClient(
                uri,
                registration.Secret,
                protector,
                _options.RequestTimeout,
                _options.MaximumRestResponseBytes);
        }

        var handler = handlerFactory()
            ?? throw new InvalidOperationException("The mobile-app webhook handler factory returned null.");
        try
        {
            return new HomeAssistantMobileAppWebhookClient(
                uri,
                registration.Secret,
                protector,
                _options.RequestTimeout,
                _options.MaximumRestResponseBytes,
                handler);
        }
        catch
        {
            handler.Dispose();
            throw;
        }
    }

    private static bool IsNullOrWhiteSpace(
        string? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return true;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(value[index]))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static void RequireHttpUri(
        Uri? value,
        string name,
        CancellationToken cancellationToken)
    {
        if (value is null) return;
        HomeAssistantJson.ThrowIfStringTraversalCanceled(value.OriginalString, cancellationToken);
        if (!value.IsAbsoluteUri
            || (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(value.UserInfo))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an invalid mobile-app " + name + ".");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
