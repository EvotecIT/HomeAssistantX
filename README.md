# HomeAssistantX - Home Assistant for .NET and PowerShell

HomeAssistantX is a typed, event-driven Home Assistant client for .NET and
PowerShell. It connects the native Home Assistant hierarchy—floors, areas,
devices, entities, states, and actions—so applications and scripts can discover
an unfamiliar house before operating it.

The same engine owns REST, WebSocket notifications, authentication, reconnect,
troubleshooting, Supervisor operations, typed everyday controls, and protected
raw access for custom integrations.

## 📦 NuGet Package

[![nuget downloads](https://img.shields.io/nuget/dt/HomeAssistantX?label=nuget%20downloads)](https://www.nuget.org/packages/HomeAssistantX)
[![nuget version](https://img.shields.io/nuget/v/HomeAssistantX)](https://www.nuget.org/packages/HomeAssistantX)

## 💻 PowerShell Module

[![powershell gallery version](https://img.shields.io/powershellgallery/v/HomeAssistantX.svg)](https://www.powershellgallery.com/packages/HomeAssistantX)
[![powershell gallery platforms](https://img.shields.io/powershellgallery/p/HomeAssistantX.svg)](https://www.powershellgallery.com/packages/HomeAssistantX)
[![powershell gallery downloads](https://img.shields.io/powershellgallery/dt/HomeAssistantX.svg)](https://www.powershellgallery.com/packages/HomeAssistantX)

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

- joined floor, area, device, entity, live-state, integration, and action
  inventory
- exact friendly-name or native-ID resolution with ambiguity errors instead of
  silent guesses
- typed light, switch, climate, cover, media-player, remote, and lock controls
- runtime action discovery, including descriptions, fields, examples, defaults,
  and selectors supplied by Home Assistant and installed integrations
- the documented Home Assistant REST API: state, history, logbook, actions,
  events, templates, calendars, cameras, intents, and conversations
- WebSocket events and reconnect-safe state notifications without polling
- persistent and notify-entity messages, plus reconnect-safe persistent-notification updates
- calendar discovery, timed/all-day event management, and live event-list updates
- labels and scoped categories, including create/update/delete with explicit nullable-field clearing
- Energy preferences, validation, solar forecasts, fossil-energy calculations, Recorder long-term statistics, and maintenance
- typed current weather observations plus daily, hourly, and twice-daily push forecasts
- typed camera state, bounded/scaled snapshots, HLS and signed MJPEG paths,
  stream capabilities, preferences, and reconnect-safe state updates
- Home Assistant-native media-source and media-player browse, search, and resolve APIs
- frontend panels plus Lovelace dashboard, configuration, and resource management
- automation runtime state and execution kept separate from administrator-managed definitions
- OAuth authorization, proactive and rejection-triggered refresh, revocation,
  and host-owned token persistence
- logs, Repairs, system health, diagnostics, traces, integrations, and updates
- Home Assistant OS and Supervisor information, logs, jobs, backups, apps,
  updates, and restarts
- Windows PowerShell 5.1 and PowerShell 7 on Windows, macOS, and Linux
- .NET Framework 4.7.2, .NET Standard 2.0, and .NET 10 from one package

The endpoint-by-endpoint contract is maintained in the
[Home Assistant support matrix](Docs/SUPPORT.md).

## PowerShell and C# entry points

PowerShell cmdlets are thin task-oriented surfaces over the .NET engine. The
generated [command reference](Docs/README.md) covers every command and parameter
set.

| Task | PowerShell | C# owner |
| --- | --- | --- |
| Connect and keep a runspace default | `Connect-HomeAssistant`, `Get-HomeAssistantConnection` | `HomeAssistantConnection`, `HomeAssistantClient` |
| Discover floors and rooms | `Get-HomeAssistantFloor`, `Get-HomeAssistantArea` | `client.Inventory` |
| Discover devices and joined entities | `Get-HomeAssistantDevice`, `Get-HomeAssistantEntity` | `client.Inventory` |
| Inspect available actions and fields | `Get-HomeAssistantAction` | `client.Services.GetActionsAsync` |
| Control common domains | `Set-HomeAssistantLight`, `Set-HomeAssistantSwitch`, `Set-HomeAssistantClimate`, `Set-HomeAssistantCover`, `Set-HomeAssistantMediaPlayer`, `Invoke-HomeAssistantRemote`, `Set-HomeAssistantLock` | `client.Controls` |
| Invoke integration-specific actions | `Invoke-HomeAssistantAction` | `client.Services` |
| Send and receive notifications | `Get-HomeAssistantNotification`, `Send-HomeAssistantNotification`, `Receive-HomeAssistantNotification` | `client.Notifications` |
| Work with calendars | `Get-HomeAssistantCalendar`, `Get-HomeAssistantCalendarEvent`, `Set-HomeAssistantCalendarEvent`, `Receive-HomeAssistantCalendarEvent` | `client.Calendars` |
| Work with labels and categories | `Get/Set/Remove-HomeAssistantLabel`, `Get/Set/Remove-HomeAssistantCategory` | `client.Registries` |
| Inspect Energy and Recorder data | `Get-HomeAssistantEnergy`, `Get-HomeAssistantStatistic`, `Get-HomeAssistantHistory`, `Get-HomeAssistantLogbook` | `client.Energy`, `client.Recorder`, `client.Rest` |
| Read and stream weather | `Get-HomeAssistantWeather`, `Receive-HomeAssistantWeatherForecast` | `client.Weather` |
| View cameras and export snapshots | `Get/Set-HomeAssistantCamera`, `Export-HomeAssistantCameraSnapshot` | `client.Cameras` |
| Browse and resolve playable media | `Get-HomeAssistantMedia` | `client.Media` |
| Manage Lovelace dashboards | `Get/Set/Remove-HomeAssistantDashboard` | `client.Dashboards` |
| Inspect, run, or edit automations | `Get/Invoke/Set/Remove-HomeAssistantAutomation` | `client.Automations` |
| Receive general events | `Receive-HomeAssistantEvent` | `client.Events`, `client.States` |
| Inspect and troubleshoot | `Get-HomeAssistantInfo`, `Get-HomeAssistantLog`, `Get-HomeAssistantIssue`, `Get-HomeAssistantTrace`, `Export-HomeAssistantDiagnostic` | `client.Operations` |
| Inspect Supervisor and OS | `Get-HomeAssistantApp`, `Get-HomeAssistantBackup`, `Get-HomeAssistantJob`, `Get-HomeAssistantUpdate` | `client.Supervisor` |
| Use evolving/custom APIs | `Invoke-HomeAssistantAction -Data` | `client.Rest.SendAsync`, `client.WebSocket.RequestAsync` |

HomeAssistantX owns the Home Assistant protocol and joined provider inventory.
Applications still map that data into their own product models and safety/UI
policy:

```text
Home Assistant -> HomeAssistantX -> product mapper -> application model -> UI
```

## 📦 Installation

### .NET

```bash
dotnet add package HomeAssistantX
```

### PowerShell

```powershell
Install-Module -Name HomeAssistantX -AllowClobber -Force
```

| Package detail | Value |
| --- | --- |
| NuGet / PowerShell name | `HomeAssistantX` |
| .NET targets | `net472`, `netstandard2.0`, `net10.0` |
| PowerShell hosts | Windows PowerShell 5.1, PowerShell 7 |
| Native dependencies | None |
| License | MIT |

## 🚀 PowerShell quick start

### Connect and discover an unfamiliar house

`Connect-HomeAssistant` validates REST and WebSocket access, returns the
connection, and stores it as the default for the current PowerShell runspace.
Other commands use that default when `-Connection` is omitted.

```powershell
$token = (Get-Content -LiteralPath $env:HOME_ASSISTANT_TOKEN_FILE -Raw).Trim()
Connect-HomeAssistant -Uri 'https://home.example.net' -AccessToken $token | Out-Null

Get-HomeAssistantInfo
Get-HomeAssistantFloor
Get-HomeAssistantArea
Get-HomeAssistantArea -Floor 'Ground Floor'
Get-HomeAssistantDevice -Area Kitchen
Get-HomeAssistantEntity -Area Kitchen
Get-HomeAssistantEntity -Area Kitchen -Domain light
```

Home Assistant calls a physical location an **area**. `-Room` is accepted as an
alias where it helps interactive use. Entity area assignment follows Home
Assistant: an entity's own area wins; otherwise it inherits its device's area.

Discovery objects retain Home Assistant's full friendly names, registry aliases,
and native IDs. Every explicit `-Entity` value must resolve exactly once;
ambiguous or missing names fail with candidate IDs instead of producing a
partial selection.

Entity, device, area, and floor discovery remains available to non-administrator
users. When Home Assistant denies administrator-only configuration-entry
enrichment, the joined inventory reports
`IsConfigEntryEnrichmentAvailable = false` and leaves integration details empty
instead of failing otherwise permitted entity reads.

### See what an entity can do

```powershell
$actions = Get-HomeAssistantAction -Entity 'Kitchen light'
$actions | Select-Object Domain, Action, Name, Description

$turnOn = Get-HomeAssistantAction -Domain light -Action turn_on
$turnOn.Fields | Select-Object Field, Name, Description, Required, Example, Selector
```

The action catalog comes from the connected Home Assistant instance. It
therefore includes actions and fields registered by custom integrations, not
only the domains known when HomeAssistantX was built.

### Use typed everyday controls

```powershell
# Every light in the Kitchen area
Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -WhatIf
Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -Confirm:$false

# One light by friendly name or entity ID
Set-HomeAssistantLight -Entity 'Kitchen island' -Power Off
Set-HomeAssistantLight -Entity light.kitchen_island -Power On -RgbColor 255,180,90

# Discovery objects can flow directly into typed controls
Get-HomeAssistantEntity -Area Kitchen -Domain light |
    Set-HomeAssistantLight -Power Off

Set-HomeAssistantSwitch -Area Utility -Power On
Set-HomeAssistantClimate -Entity 'Downstairs thermostat' -HvacMode heat -Temperature 21.5
Set-HomeAssistantCover -Entity 'Kitchen blind' -PositionPercent 60
Set-HomeAssistantMediaPlayer -Area LivingRoom -VolumePercent 30 -Playback Play
Set-HomeAssistantMediaPlayer -Entity media_player.kitchen `
    -MediaContentId 'media-source://media_source/local/dinner.mp3' `
    -MediaContentType music -Announce
Invoke-HomeAssistantRemote -Entity remote.harmony -Action TurnOn -Activity 'Watch TV'
Invoke-HomeAssistantRemote -Entity remote.living_room `
    -Action SendCommand -Command Power -RepeatCount 2 -WhatIf
Set-HomeAssistantLock -Entity lock.front_door -Action Unlock -WhatIf
```

Typed commands validate common values, expose PowerShell completion for enums,
resolve friendly targets, and support `-WhatIf` / `-Confirm`. Lock operations
use high confirmation impact. The calling application or script still owns its
authorization policy for consequential actions.

Range-based climate calls require both low and high temperatures and cannot be
combined with a scalar temperature. Media-player `Off` and `Toggle` are
standalone power operations; combine additional playback, source, or volume
changes with `Power On` or omit `Power` so later actions cannot reverse a
requested shutdown. Starting media content is already a playback operation, so
content and a separate `Playback` or `SeekSeconds` action cannot be combined in
one call. Queue placement and announcement playback are alternative Home
Assistant modes. Remote power, send, learn, and delete operations share one
typed command whose parameters are validated for the selected action.
Light color temperature and RGB are alternative representations and cannot be
sent together.

### Keep explicit connections when you need them

The default is runspace-local, not process-wide. Jobs and parallel runspaces do
not inherit it. Explicit pipeline use remains available, and `-NoDefault` is
useful when working with more than one home. Establishing another default
disposes the previous default connection; a `-NoDefault` connection remains
caller-owned:

```powershell
$home = Get-HomeAssistantConnection
$lab = Connect-HomeAssistant -Uri 'https://lab.example.net' `
    -AccessToken $labToken -Name Lab -NoDefault

$home | Get-HomeAssistantEntity -Domain light
$lab  | Get-HomeAssistantEntity -Domain light
$lab  | Get-HomeAssistantEntity -Domain light |
    Set-HomeAssistantLight -Power Off
$lab  | Disconnect-HomeAssistant

# Disconnects the current runspace default
Disconnect-HomeAssistant
```

Entities emitted by the module retain their source connection for typed-control
pipeline use. Passing a different explicit `-Connection`, combining entities
from different homes in one action, or piping an entity without provenance and
without `-Connection` fails before dispatch.

### Use the generic escape hatch for custom actions

Typed commands cover the common domains. `Invoke-HomeAssistantAction` remains
the direct path for integration-specific actions and uncommon fields discovered
with `Get-HomeAssistantAction`:

```powershell
Get-HomeAssistantAction -Domain vacuum

Invoke-HomeAssistantAction vacuum send_command `
    -EntityId vacuum.downstairs `
    -Data @{ command = 'clean_spot'; params = @{ repeats = 2 } } `
    -WhatIf
```

### Receive notifications without polling

```powershell
Get-HomeAssistantNotification
Send-HomeAssistantNotification -Persistent -Message 'Garage is open' `
    -Title Security -NotificationId garage-open
Send-HomeAssistantNotification -Area Kitchen -Message 'Dinner is ready' -WhatIf
Receive-HomeAssistantNotification -Count 1 -TimeoutSeconds 30

Receive-HomeAssistantEvent -EventType automation_triggered

$nextChange = Receive-HomeAssistantEvent `
    -EntityId light.kitchen, lock.front_door `
    -Count 1 -TimeoutSeconds 30
```

The receive commands hold bounded WebSocket subscriptions until canceled or
their count/timeout is reached. Persistent notifications emit an immediate
current snapshot and then added, updated, or removed changes. Reconnect-safe
state subscriptions are also available through the .NET `client.States` API.

### Work with calendars, labels, and categories

```powershell
Get-HomeAssistantCalendar
Get-HomeAssistantCalendarEvent -EntityId calendar.home `
    -EndTime (Get-Date).AddDays(7)

Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Dinner `
    -StartTime '2026-08-27T18:00:00+02:00' `
    -EndTime '2026-08-27T20:00:00+02:00' -WhatIf
Receive-HomeAssistantCalendarEvent -EntityId calendar.home -Count 1

Get-HomeAssistantLabel
Set-HomeAssistantLabel -LabelId security -ClearColor -Description 'Safety devices'
Get-HomeAssistantCategory -Scope automation
Set-HomeAssistantCategory -Scope automation -Name Comfort -Icon mdi:sofa
```

Calendar parameter sets distinguish timed and all-day events, while `-Uid`
selects update behavior. Label/category setters distinguish create from update;
`-ClearColor`, `-ClearDescription`, and `-ClearIcon` explicitly send `null`
instead of silently omitting a field.

### Inspect Energy, statistics, history, and weather

```powershell
Get-HomeAssistantEnergy
Get-HomeAssistantEnergy -Info
Get-HomeAssistantEnergy -Validation
Get-HomeAssistantEnergy -SolarForecast
Get-HomeAssistantEnergy -FossilConsumption `
    -StartTime (Get-Date).AddDays(-1) -EndTime (Get-Date) `
    -EnergyStatisticId sensor.grid_energy `
    -Co2StatisticId sensor.co2_intensity -Period Hour

Get-HomeAssistantStatistic -Kind Sum
Get-HomeAssistantStatistic -StatisticId sensor.grid_energy `
    -StartTime (Get-Date).AddDays(-7) -Period Hour -Type Change, Sum
Get-HomeAssistantHistory sensor.grid_energy -StartTime (Get-Date).AddHours(-6)
Get-HomeAssistantLogbook -StartTime (Get-Date).Date -EntityId light.kitchen
Test-HomeAssistantStatistic

Get-HomeAssistantWeather
Get-HomeAssistantWeather weather.home -Forecast -ForecastType Daily
Receive-HomeAssistantWeatherForecast weather.home -ForecastType Hourly -Count 1
```

Energy sources and solar forecasts may contain integration-defined fields, so
their named APIs preserve those portions as JSON while typing stable Home
Assistant fields. Recorder mutation commands use high-impact confirmation and
validate their parameter sets before `-WhatIf`; clearing statistics and purging
Recorder data are deliberately explicit operations.

### Work with cameras, media, dashboards, and automations

```powershell
Get-HomeAssistantCamera
Get-HomeAssistantCamera camera.front -Capabilities
Get-HomeAssistantCamera camera.front -Stream
Export-HomeAssistantCameraSnapshot camera.front ./front.jpg `
    -Width 1280 -Height 720

$library = Get-HomeAssistantMedia
Get-HomeAssistantMedia -PlayerEntityId media_player.kitchen -Search dinner
Get-HomeAssistantMedia -Resolve `
    -MediaContentId 'media-source://media_source/local/dinner.mp3'

Get-HomeAssistantDashboard
Get-HomeAssistantDashboard -Configuration -UrlPath house-main
Set-HomeAssistantDashboard -ConfigurationJson $dashboardJson `
    -UrlPath house-main -WhatIf

Get-HomeAssistantAutomation
Invoke-HomeAssistantAutomation automation.morning -WhatIf
Get-HomeAssistantAutomation morning-routine -Configuration
Set-HomeAssistantAutomation morning-routine $automationJson -WhatIf
```

Camera snapshots are bounded by the connection response limit and exported
through an atomic local-file replacement. Media trees retain provider-specific
fields while exposing stable browse/play identifiers. Lovelace writes require
an administrator and storage-backed dashboards/resources. Automation entity
execution is a runtime action; editing uses the separate administrator-only
configuration identifier and never happens implicitly during `Invoke`.

### Troubleshoot and administer

```powershell
Get-HomeAssistantInfo -Capabilities
Get-HomeAssistantInfo -Health
Get-HomeAssistantLog | Where-Object Level -In Error, Warning
Get-HomeAssistantIssue
Get-HomeAssistantIntegration -Domain mqtt
Get-HomeAssistantTrace automation morning_lights
Test-HomeAssistantConfiguration
Export-HomeAssistantDiagnostic entry_id ./diagnostic.json

# Home Assistant OS / Supervised installation and suitable permissions required
Get-HomeAssistantInfo -Supervisor
Get-HomeAssistantApp
Get-HomeAssistantBackup
Get-HomeAssistantUpdate -AvailableOnly
```

Logs and diagnostics may contain sensitive installation information. Treat the
output as confidential and review it before sharing.

## 🚀 .NET quick start

### Discover floors, rooms, devices, and entities

```csharp
using HomeAssistantX;
using HomeAssistantX.Inventory;

var baseUri = new Uri(Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL")!);
var token = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")!;

using var client = HomeAssistantClient.Create(baseUri, token);
var inventory = await client.Inventory.GetSnapshotAsync();

foreach (var floor in inventory.Floors)
{
    Console.WriteLine($"{floor.Name}: {floor.Areas.Count} areas");
}

var kitchenLights = await client.Inventory.GetEntitiesAsync(
    new HomeAssistantEntityQuery
    {
        Area = "Kitchen",
        Domain = "light",
        AvailableOnly = true
    });

foreach (var light in kitchenLights)
{
    Console.WriteLine($"{light.Name} [{light.EntityId}] = {light.State}");
}
```

The joined snapshot contains raw registry/state objects as well as the resolved
view, so consumers can use new or integration-specific fields without waiting
for another model.

### Use typed controls

```csharp
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

var kitchen = client.Inventory.ResolveArea(inventory, "Kitchen");
var result = await client.Controls.Lights.TurnOnAsync(
    HomeAssistantTarget.ForArea(kitchen.AreaId),
    new HomeAssistantLightOptions
    {
        BrightnessPercent = 45,
        Transition = TimeSpan.FromSeconds(1)
    });
```

`client.Controls` exposes focused clients for lights, switches, climate,
covers, media players, remotes, and locks. `client.Notifications`,
`client.Calendars`, `client.Registries`, `client.Cameras`, `client.Media`,
`client.Dashboards`, and `client.Automations` own their corresponding platform
contracts. `client.Services` retains the generic fluent
action builder for every other domain and custom integration.

Typed controls use WebSocket calls by default. Hosts that deliberately keep
commands on the REST transport can set
`HomeAssistantClientOptions.ControlServiceCallTransport` to
`HomeAssistantServiceCallTransport.Rest`; the same typed validation and payload
ownership apply on either transport. Explicit `Services.CallAsync` and
`Services.CallRestAsync` calls continue to select their named transport.

### Receive state changes

```csharp
using HomeAssistantX.States;

using var subscription = await client.States.SubscribeAsync(
    HomeAssistantStateFilter.ForDomains("light", "switch", "lock"),
    (change, cancellationToken) =>
    {
        Console.WriteLine($"{change.EntityId}: {change.CurrentState?.State ?? "removed"}");
        return Task.CompletedTask;
    });

await subscription.Completion;
```

The state client subscribes before loading its initial snapshot, buffers changes
that race the snapshot, reconnects, and reports changes missed while disconnected.

## Authentication and raw access

For production applications, keep OAuth tokens in Keychain, Credential Manager,
or another platform credential store. `HomeAssistantOAuthClient` builds
authorization URLs, exchanges codes, refreshes access tokens, and revokes
refresh tokens. `RefreshingAccessTokenProvider` refreshes shortly before expiry
and once when Home Assistant unexpectedly rejects an otherwise unexpired access
token. Concurrent requests share the same refresh, and the host-provided
persistence callback receives the replacement before it becomes active.

REST requests retry once after HTTP 401. A rejected WebSocket token is refreshed
and authenticated on a new socket because Home Assistant closes the rejected
session. `StaticAccessTokenProvider` does not retry rejected long-lived tokens;
the authentication failure is returned to the caller. Custom providers can opt
into the same one-retry policy by implementing
`IHomeAssistantAccessTokenRecovery`. HomeAssistantX does not log or persist
credentials itself.

Custom REST and WebSocket calls retain authentication, same-origin checks,
timeouts, bounded response sizes, and classified failures. WebSocket connections
negotiate Home Assistant message coalescing as command 1 and bound each received
batch by both bytes and message count:

```csharp
var preferences = await client.WebSocket.RequestAsync("energy/get_prefs");
var image = await client.Rest.GetBytesAsync("api/camera_proxy/camera.front_door");
```

## Boundaries

- Supervisor features require Home Assistant OS or a supervised installation
  and suitable permissions.
- State writes through `/api/states` change Home Assistant's state
  representation; they do not control the physical device.
- Restore, wipe, recovery, and host shutdown are not convenience operations.
- Refresh-capable providers make one recovery attempt after a rejected access
  token. Static tokens and failed refreshes surface an authentication failure;
  there is no hidden infinite retry loop.
- HomeKit uses a different protocol and credential model.
- Product-specific device normalization, UI, and action policy stay in the
  consuming application.

See [Docs/SUPPORT.md](Docs/SUPPORT.md) for precise coverage and
[Docs/ROADMAP.md](Docs/ROADMAP.md) for open work.

## 🧪 Build and test

```powershell
dotnet restore HomeAssistantX.slnx
dotnet build HomeAssistantX.slnx --configuration Release --no-restore
dotnet test HomeAssistantX.Tests/HomeAssistantX.Tests.csproj --configuration Release --no-build
dotnet pack HomeAssistantX/HomeAssistantX.csproj --configuration Release --no-build
./Tests/PowerShell/Test-Module.ps1 `
    -AssemblyPath ./HomeAssistantX.PowerShell/bin/Release/net10.0/HomeAssistantX.PowerShell.dll
```

The contract suite uses a real loopback HTTP/WebSocket peer and runs on .NET
Framework 4.7.2 and .NET 10. It proves transport framing, concurrency,
cancellation, reconnect, joined discovery, target resolution, typed payloads,
runspace defaults, `-WhatIf`, and PowerShell 5.1/7 behavior.

Optional live tests use `HOME_ASSISTANT_URL` and `HOME_ASSISTANT_TOKEN`. They
read the actual installation without calling actions or changing the home.

## 📖 Documentation and support

- [Generated PowerShell command reference](Docs/README.md)
- [PowerShell design guide](Docs/POWERSHELL.md)
- [Home Assistant support matrix](Docs/SUPPORT.md)
- [Roadmap](Docs/ROADMAP.md)
- [Runnable .NET example](HomeAssistantX.Examples/Program.cs)
- [Issues](https://github.com/EvotecIT/HomeAssistantX/issues)

## 📄 License

HomeAssistantX is licensed under the [MIT License](LICENSE).

HomeAssistantX is an independent project and is not affiliated with or endorsed
by the Home Assistant project.
