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

HomeAssistantX also ships a thin binary PowerShell module for Windows
PowerShell 5.1 and PowerShell 7 on Windows, macOS, and Linux. It exposes 21
task-oriented commands over the same .NET engine instead of creating one
cmdlet per Home Assistant service.

The first PowerShell Gallery release has not been published yet. A source
checkout can build and install the module locally with:

```powershell
./Build/Build-Module.ps1 -RunMode Build
Import-Module HomeAssistantX
```

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
- system logs, Repairs issues, system health, diagnostics, traces,
  configuration-entry operations, and update discovery
- Supervisor and Home Assistant OS information, logs, jobs, backups, apps,
  updates, restarts, and protected raw access
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
| Inspect logs, health, Repairs, integrations, traces, updates, and diagnostics | `client.Operations` | REST / WebSocket |
| Inspect or administer Supervisor, OS, apps, backups, and jobs | `client.Supervisor` | Core proxy / Supervisor API |
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
- **PowerShell module:** implemented for Windows PowerShell 5.1 and PowerShell 7; public release pending

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

## PowerShell quick start

Connections are explicit objects. This makes multiple Home Assistant instances
safe in one session and keeps cmdlets pipeline-friendly without hidden global
state:

```powershell
$token = Get-Content -LiteralPath $env:HOME_ASSISTANT_TOKEN_FILE -Raw
$ha = Connect-HomeAssistant -Uri 'https://home.example.net' -AccessToken $token

$ha | Get-HomeAssistantInfo
$ha | Get-HomeAssistantEntity -Domain light
$ha | Get-HomeAssistantLog | Where-Object Level -In Error, Warning
$ha | Get-HomeAssistantIssue
$ha | Get-HomeAssistantUpdate -AvailableOnly
```

The command model is intentionally small. Parameter sets express the target or
source while one cmdlet owns one task:

```powershell
# One action command for any Home Assistant domain/action and target kind
$ha | Invoke-HomeAssistantAction light turn_on -AreaId kitchen `
    -Data @{ brightness_pct = 45 } -WhatIf

# One log command for structured Core logs or bounded Supervisor-managed logs
$ha | Get-HomeAssistantLog
$ha | Get-HomeAssistantLog -Core -Tail 200
$ha | Get-HomeAssistantLog -App 'example_app' -Tail 100

# One update command for update entities and Supervisor-managed targets
$ha | Install-HomeAssistantUpdate -EntityId update.example -WhatIf
$ha | Install-HomeAssistantUpdate -Core -WhatIf
```

Mutating commands support `-WhatIf` and `-Confirm`. High-impact operations such
as updates, restarts, host reboots, and app lifecycle changes use high confirm
impact. Applications and scripts still need domain-specific policy for actions
such as unlocking, disarming, or opening access points.

### Notifications without polling

`Receive-HomeAssistantEvent` holds a WebSocket subscription open until the
pipeline is stopped. It can stream one event type, all events, or state changes
for selected entities:

```powershell
$ha | Receive-HomeAssistantEvent -EventType automation_triggered
$ha | Receive-HomeAssistantEvent -EntityId 'light.kitchen', 'lock.front_door'
$nextChange = $ha | Receive-HomeAssistantEvent -EntityId 'light.kitchen' `
    -Count 1 -TimeoutSeconds 30
```

Press Ctrl+C to cancel an open-ended stream, or use `-Count` and
`-TimeoutSeconds` for a bounded wait. The cmdlet performs bounded subscription
cleanup and does not poll the REST API.

### Troubleshooting and administration

```powershell
$ha | Get-HomeAssistantInfo -Capabilities
$ha | Get-HomeAssistantInfo -Health
$ha | Test-HomeAssistantConfiguration
$ha | Get-HomeAssistantIntegration -Domain mqtt
$ha | Get-HomeAssistantTrace automation morning_lights
$ha | Export-HomeAssistantDiagnostic entry_id ./diagnostic.json

# Home Assistant OS / Supervised installations, administrator access required
$ha | Get-HomeAssistantInfo -Supervisor
$ha | Get-HomeAssistantJob
$ha | Get-HomeAssistantBackup
```

Diagnostics and logs may contain sensitive installation information. Treat
their output as confidential and avoid attaching it unreviewed to issues.

See the [PowerShell command guide](Docs/POWERSHELL.md) for the complete command
map and parameter-set design.

## Current boundaries

HomeAssistantX intentionally does not claim every API carrying the Home
Assistant name:

- WebSocket coalesced message arrays are not enabled yet.
- Unexpected HTTP 401 responses are surfaced; the host decides whether to
  refresh or reauthorize instead of entering a hidden retry loop.
- mDNS instance discovery and native companion-app registration/webhooks are
  future adapters.
- Supervisor and Home Assistant OS operations require a supervised/OS
  installation and suitable administrator privileges. They are unavailable on
  Home Assistant Container and Core installations.
- The library models routine read, backup, update, restart, and app lifecycle
  operations. Restore, wipe, recovery, and host shutdown intentionally remain
  outside the convenience API.
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
./Tests/PowerShell/Test-Module.ps1 -AssemblyPath ./HomeAssistantX.PowerShell/bin/Release/net10.0/HomeAssistantX.PowerShell.dll
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
- [PowerShell command guide](Docs/POWERSHELL.md)
- [Roadmap](Docs/ROADMAP.md)
- [Runnable .NET example](HomeAssistantX.Examples/Program.cs)
- [Issues](https://github.com/EvotecIT/HomeAssistantX/issues)

## 📄 License

HomeAssistantX is licensed under the [MIT License](LICENSE).

HomeAssistantX is an independent project and is not affiliated with or endorsed
by the Home Assistant project.
