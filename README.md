# HomeAssistantX - Home Assistant for .NET

HomeAssistantX is a typed, event-driven .NET client for the Home Assistant REST
and WebSocket APIs. It owns authentication, transport, subscriptions,
reconnection, and protocol-safe escape hatches so applications do not need to
build a second Home Assistant client for every product.

The package targets .NET Framework 4.7.2, .NET Standard 2.0, and .NET 10.

## 📦 NuGet Package

HomeAssistantX is built and validated as one NuGet package. The first public
NuGet.org release has not been published yet; use a project reference or a
locally packed package for now.

## 💻 PowerShell Module

A thin PowerShell surface is planned, but it is not published yet. It will use
HomeAssistantX as the shared engine rather than introducing another Home
Assistant transport implementation.

## 🛠️ Project Information

[![CI](https://github.com/EvotecIT/HomeAssistantX/actions/workflows/ci.yml/badge.svg)](https://github.com/EvotecIT/HomeAssistantX/actions/workflows/ci.yml)
[![Top language](https://img.shields.io/github/languages/top/EvotecIT/HomeAssistantX.svg)](https://github.com/EvotecIT/HomeAssistantX)
[![License](https://img.shields.io/github/license/EvotecIT/HomeAssistantX.svg)](LICENSE)

## 👨‍💻 Author & Social

[![Twitter Follow](https://img.shields.io/twitter/follow/PrzemyslawKlys.svg?label=Twitter%20%40PrzemyslawKlys&style=social)](https://twitter.com/PrzemyslawKlys)
[![Blog](https://img.shields.io/badge/Blog-evotec.xyz-2A6496.svg)](https://evotec.xyz/hub)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-pklys-0077B5.svg?logo=LinkedIn)](https://www.linkedin.com/in/pklys)
[![Discord](https://img.shields.io/discord/508328927853281280?style=flat-square&label=discord%20chat)](https://evo.yt/discord)

## What it covers

- the documented Home Assistant Core REST API, including states, history,
  logbook, services/actions, events, templates, calendars, cameras, intents,
  and conversations
- authenticated WebSocket commands with concurrent request correlation,
  fragmented-message handling, and bounded message sizes
- state and event notifications without polling
- reconnect-safe state subscriptions with initial-snapshot race handling and
  missed-change reconciliation
- fluent service/action calls targeting entities, devices, areas, floors, and
  labels
- OAuth authorization, code exchange, refresh, refresh-token revocation, and
  host-owned secure token persistence
- area, floor, device, entity, and configuration-entry registry snapshots
- typed system helpers plus raw REST and WebSocket access for custom or
  fast-moving integration APIs
- .NET Framework 4.7.2, .NET Standard 2.0, and .NET 10 from one package

The exact coverage and intentional limits are maintained in the
[Home Assistant support matrix](https://github.com/EvotecIT/HomeAssistantX/blob/main/Docs/SUPPORT.md).

## .NET entry points

`HomeAssistantClient` is the main entry point. Its focused clients expose the
same authenticated connection without mixing transport code into application
models.

| Task | C# entry point | Transport |
| --- | --- | --- |
| Check the instance and read configuration | `client.Rest.CheckApiAsync`, `GetConfigurationAsync` | REST |
| Read, create, update, or delete state representations | `client.States` | REST / WebSocket |
| Receive state changes without polling | `client.States.SubscribeAsync` | WebSocket |
| Subscribe to events or triggers | `client.Events.SubscribeAsync`, `SubscribeTriggerAsync` | WebSocket |
| Discover and call services/actions | `client.Services` | REST / WebSocket |
| Read areas, floors, devices, entities, and config entries | `client.Registries.GetSnapshotAsync` | WebSocket |
| Validate configuration, inspect targets, sign paths, or process conversation | `client.System` | WebSocket |
| Use a documented REST endpoint directly | `client.Rest` | REST |
| Use an integration-specific command | `client.Rest.SendAsync`, `client.WebSocket.RequestAsync` | REST / WebSocket |
| Run OAuth authorization and token lifecycle | `HomeAssistantOAuthClient` | HTTP |

HomeAssistantX is the reusable protocol owner. Product applications should map
provider data into their own device, room, policy, and UI models:

```text
Home Assistant data -> HomeAssistantX -> product mapper -> normalized model -> application/UI
```

## 📦 Installation & Package Information

### Use a source checkout

```bash
# From a directory containing sibling MyApp and HomeAssistantX folders:
dotnet add ./MyApp/MyApp.csproj reference ./HomeAssistantX/HomeAssistantX/HomeAssistantX.csproj
```

### Build and consume a local package

```powershell
dotnet pack HomeAssistantX/HomeAssistantX.csproj `
    --configuration Release `
    --output ./artifacts/packages

dotnet add <path-to-consumer.csproj> package HomeAssistantX `
    --source ./artifacts/packages
```

After the first public release, the normal installation command will be:

```bash
dotnet add package HomeAssistantX
```

### Package information

- **Package:** `HomeAssistantX`
- **Public release:** pending
- **Target frameworks:** .NET Framework 4.7.2, .NET Standard 2.0, and .NET 10
- **License:** MIT
- **Native or platform-specific dependencies:** none
- **Compatibility dependencies:** older targets use the required .NET JSON,
  channels, HTTP, and reference-assembly compatibility packages
- **PowerShell module:** planned, not currently published

## 🚀 Quick Start

Store the Home Assistant URL and a long-lived access token outside source
control, then create one client for the application lifetime:

```csharp
using HomeAssistantX;

var baseUrl = Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL");
var accessToken = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN");

if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
    || string.IsNullOrWhiteSpace(accessToken))
{
    throw new InvalidOperationException(
        "Set HOME_ASSISTANT_URL and HOME_ASSISTANT_TOKEN before connecting.");
}

using var client = HomeAssistantClient.Create(baseUri, accessToken);

var api = await client.Rest.CheckApiAsync();
var configuration = await client.Rest.GetConfigurationAsync();
var states = await client.States.GetAllAsync();

await client.WebSocket.ConnectAsync();
await client.WebSocket.PingAsync();

Console.WriteLine(
    $"{api.Message} {configuration.LocationName}: {states.Count} entity states");
```

For production applications, implement
`IHomeAssistantAccessTokenProvider` over Keychain, Credential Manager, or
another platform credential store instead of keeping tokens in configuration
files.

## Receive changes without polling

The state client subscribes before loading its initial snapshot. It buffers
changes that race the snapshot, restores the subscription after a disconnect,
and reports state changes missed while the connection was unavailable.

```csharp
using HomeAssistantX.States;

using var subscription = await client.States.SubscribeAsync(
    HomeAssistantStateFilter.ForDomains("light", "switch", "lock"),
    (change, cancellationToken) =>
    {
        Console.WriteLine(
            $"{change.EntityId}: {change.CurrentState?.State ?? "removed"}");
        return Task.CompletedTask;
    });

await subscription.Completion;
```

Use `client.Events.SubscribeAsync(...)` for any Home Assistant event type.
Subscriptions have bounded buffers, a completion task, and explicit cleanup
through `StopAsync` or `Dispose`.

## Call services/actions

The fluent request builder keeps Home Assistant's domain/service model visible
while making multi-kind targets straightforward:

```csharp
using HomeAssistantX.Services;

var call = HomeAssistantServiceCall.Create("light", "turn_on")
    .ForArea("kitchen")
    .WithData("brightness_pct", 45);

var result = await client.Services.CallAsync(call);        // WebSocket
// var result = await client.Services.CallRestAsync(call); // REST
```

Applications remain responsible for authorization and confirmation around
consequential actions such as unlocking, disarming, opening a gate, or opening
a garage door.

## Read history, templates, and camera data

The typed REST surface covers the endpoint families documented by Home
Assistant Core:

```csharp
using HomeAssistantX.Rest;

var history = await client.Rest.GetHistoryAsync(
    new HomeAssistantHistoryQuery("sensor.outdoor_temperature")
    {
        StartTime = DateTimeOffset.UtcNow.AddHours(-24),
        MinimalResponse = true,
        SignificantChangesOnly = true
    });

var rendered = await client.Rest.RenderTemplateAsync(
    "{{ states('sensor.outdoor_temperature') }}");

var cameraBytes = await client.Rest.GetCameraImageAsync("camera.front_door");
```

State writes through `/api/states` change Home Assistant's state
representation. They do not control the physical device. Use a service/action
call when the intent is to operate a device.

## OAuth and token refresh

`HomeAssistantOAuthClient` supports:

- building the authorization URI with caller-provided anti-forgery state
- exchanging an authorization code
- refreshing access tokens
- revoking a refresh token and its derived access tokens
- separating credential rejection from transient HTTP and connection failures

`RefreshingAccessTokenProvider` serializes concurrent refresh attempts and
invokes a host callback after refresh so replacement tokens can be persisted in
the platform's secure store. HomeAssistantX does not log or persist tokens.

An application still owns its OAuth callback, redirect URI registration, state
validation, and secure credential storage.

## Raw escape hatches

Custom integrations and frontend commands often evolve faster than a reusable
model. Raw access remains available without bypassing authentication, timeout,
same-origin, error-classification, or response-size protections:

```csharp
var preferences = await client.WebSocket.RequestAsync("energy/get_prefs");
var snapshot = await client.Rest.GetBytesAsync(
    "api/camera_proxy/camera.front_door");
```

Authenticated absolute REST URLs are accepted only when their scheme, host,
and port match the configured Home Assistant instance. This prevents forwarding
a bearer token to another origin.

Unknown JSON fields and entity attributes are preserved so a Home Assistant
update or custom integration does not require an immediate package release.

## Connection behavior and diagnostics

`HomeAssistantClientOptions` controls request and connection timeouts,
keep-alive intervals, reconnect delays, subscription capacity, and maximum REST
or WebSocket message sizes. Defaults are bounded and can be lowered by the
host.

Failures are classified for callers:

- `HomeAssistantAuthenticationException` - invalid or rejected credentials
- `HomeAssistantConnectionException` - timeouts, transport failures, transient
  OAuth responses, or interrupted response streams
- `HomeAssistantProtocolException` - invalid or unexpected protocol data
- `HomeAssistantCommandException` - a Home Assistant command or REST request
  was rejected

Implement `IHomeAssistantDiagnosticsSink` to receive connection, reconnect, and
protocol diagnostics without coupling the library to a logging framework.

## PowerShell direction

A PowerShell module is planned after the .NET API settles. It will be a thin
command surface over HomeAssistantX rather than a separate REST/WebSocket
implementation. That keeps authentication, notifications, reconnect behavior,
error classification, and Home Assistant compatibility in one owner.

No HomeAssistantX PowerShell module is published today. Command names,
PowerShell version support, installation, and examples will be documented when
that module exists.

## Current boundaries

HomeAssistantX intentionally does not claim every API carrying the Home
Assistant name:

- WebSocket coalesced message arrays are not enabled yet.
- Unexpected HTTP 401 responses are surfaced; the host decides whether to
  refresh or reauthorize instead of entering a hidden retry loop.
- mDNS instance discovery and native companion-app registration/webhooks are
  future adapters.
- Supervisor and Home Assistant OS administration APIs are outside the Core
  client boundary.
- HomeKit is a separate protocol and credential model.
- Product-specific device normalization belongs in the consuming application.

See [Docs/SUPPORT.md](Docs/SUPPORT.md) for the endpoint-by-endpoint status and
[Docs/ROADMAP.md](Docs/ROADMAP.md) for work planned before a stable release.

## 🧪 Build and Test

```powershell
dotnet restore HomeAssistantX.slnx
dotnet build HomeAssistantX.slnx --configuration Release --no-restore
dotnet test HomeAssistantX.Tests/HomeAssistantX.Tests.csproj --configuration Release --no-build
dotnet pack HomeAssistantX/HomeAssistantX.csproj --configuration Release --no-build
```

The contract suite uses a real loopback HTTP/WebSocket peer. It covers OAuth
forms and transport failures, fragmented messages, concurrent out-of-order
responses, state changes racing the initial snapshot, cancellation,
disconnect/reconnect, and missed-state reconciliation on .NET Framework 4.7.2
and .NET 10.

Optional read-only live tests use `HOME_ASSISTANT_URL` and
`HOME_ASSISTANT_TOKEN`. They validate a real instance without calling services
or changing the home.

## 📖 Documentation & Support

- [Home Assistant support matrix](Docs/SUPPORT.md)
- [Roadmap](Docs/ROADMAP.md)
- [Runnable .NET example](HomeAssistantX.Examples/Program.cs)
- [Issues](https://github.com/EvotecIT/HomeAssistantX/issues)

## 📄 License

HomeAssistantX is licensed under the [MIT License](LICENSE).

HomeAssistantX is an independent project and is not affiliated with or endorsed
by the Home Assistant project.
