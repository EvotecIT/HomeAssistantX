# HomeAssistantX

HomeAssistantX is a typed, event-driven .NET client for the Home Assistant REST and WebSocket APIs. It gives applications one reusable transport layer while leaving device normalization, user policy, credential storage, and UI behavior in the consuming product.

The single `HomeAssistantX` package targets .NET Framework 4.7.2, .NET Standard 2.0, and .NET 10.

See the [support matrix](https://github.com/EvotecIT/HomeAssistantX/blob/main/Docs/SUPPORT.md) for exact REST, WebSocket, OAuth, notification, registry, and platform boundaries.

## Install

```powershell
dotnet add package HomeAssistantX
```

## Connect and read state

```csharp
using HomeAssistantX;

using var client = HomeAssistantClient.Create(
    new Uri("https://home.example.net"),
    Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")!);

var configuration = await client.Rest.GetConfigurationAsync();
var states = await client.States.GetAllAsync();
Console.WriteLine($"{configuration.LocationName}: {states.Count} entities");
```

Production applications should implement `IHomeAssistantAccessTokenProvider` over their platform credential store rather than keeping tokens in configuration files.

For interactive sign-in, `HomeAssistantOAuthClient` builds the authorization URI and implements code exchange, refresh, and refresh-token revocation. `RefreshingAccessTokenProvider` serializes proactive refresh and calls back into the host to persist replacement tokens securely.

## Receive changes without polling

`HomeAssistantStateClient` subscribes before loading its initial snapshot, buffers in-flight changes, and restores the subscription after a dropped connection. Reconnect reconciliation reports changes that occurred while the connection was unavailable.

```csharp
using HomeAssistantX.States;

using var subscription = await client.States.SubscribeAsync(
    HomeAssistantStateFilter.ForDomains("light", "switch"),
    (change, cancellationToken) =>
    {
        Console.WriteLine($"{change.EntityId}: {change.CurrentState?.State ?? "removed"}");
        return Task.CompletedTask;
    });

await subscription.Completion;
```

Use `client.Events.SubscribeAsync(...)` for any Home Assistant event type. Each subscription has a bounded buffer, a completion task, and explicit cancellation through `StopAsync` or `Dispose`.

## Call actions/services

The fluent request builder handles entity, device, area, floor, and label targets without hiding Home Assistant's domain/service model.

```csharp
using HomeAssistantX.Services;

var call = HomeAssistantServiceCall.Create("light", "turn_on")
    .ForArea("kitchen")
    .WithData("brightness_pct", 45);

var result = await client.Services.CallAsync(call);       // WebSocket
// var result = await client.Services.CallRestAsync(call); // REST
```

Applications remain responsible for confirmations and authorization around consequential actions such as unlocking, disarming, or opening access points.

## API shape

- `Rest` — typed API/config/state/service methods plus generic JSON and binary requests
- `WebSocket` — authenticated, concurrent command multiplexing and raw commands
- `States` — REST reads, live snapshot, filtered push updates, and reconnect reconciliation
- `Services` — service discovery and fluent REST/WebSocket calls
- `Events` — event-bus subscriptions without polling
- `Registries` — area, floor, device, entity, and config-entry snapshots
- `System` — WebSocket config, panels, validation, targets, exposure settings, signed paths, and conversation commands

`Rest` also covers the complete documented Core REST families: components, event catalogs, history, logbook, state representations, camera images, calendars, templates, configuration checks, intents, and conversations.

Unknown JSON fields and entity attributes are preserved so a Home Assistant update or custom integration does not require an immediate package release. Product-specific models belong above this layer.

## Raw escape hatches

APIs that are integration-specific or evolve faster than the common model remain available without bypassing authentication, timeout, error, or connection handling:

```csharp
var energy = await client.WebSocket.RequestAsync("energy/get_prefs");
var snapshot = await client.Rest.GetBytesAsync("api/camera_proxy/camera.front_door");
```

Authenticated absolute REST URLs are accepted only when they have the same scheme, host, and port as the configured Home Assistant instance. This prevents accidentally forwarding a bearer token to another origin.

REST responses and WebSocket messages are size-bounded. The defaults are 64 MiB for each transport so large entity and registry snapshots work without permitting unbounded allocation; applications can lower `MaximumRestResponseBytes` or `MaximumWebSocketMessageBytes` in `HomeAssistantClientOptions`.

## Validation

```powershell
dotnet restore HomeAssistantX.slnx
dotnet build HomeAssistantX.slnx --configuration Release --no-restore
dotnet test HomeAssistantX.Tests/HomeAssistantX.Tests.csproj --configuration Release --no-build
dotnet pack HomeAssistantX/HomeAssistantX.csproj --configuration Release --no-build
```

The contract suite uses a real loopback HTTP/WebSocket peer, including fragmented messages, out-of-order concurrent responses, state changes racing the initial snapshot, disconnect/reconnect, and missed-state reconciliation. It does not reduce protocol proof to mocked method calls.

Optional read-only live tests use `HOME_ASSISTANT_URL` and `HOME_ASSISTANT_TOKEN`. They read configuration/state and validate authenticated WebSocket commands and subscription setup; they do not call services or mutate the home.

HomeAssistantX is an independent project and is not affiliated with or endorsed by the Home Assistant project.
