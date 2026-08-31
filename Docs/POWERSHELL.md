# HomeAssistantX PowerShell

The HomeAssistantX module is a thin task-oriented shell over the .NET client.
Windows PowerShell 5.1 loads the `net472` binary. PowerShell 7 uses the portable
`netstandard2.0` payload on Windows, macOS, and Linux; `net10.0` is also built
and validated for current hosts and direct binary consumers.

## Connection model

`Connect-HomeAssistant` validates REST and WebSocket access, returns a
`HomeAssistantConnection`, and stores it as the current runspace default.
Commands use an explicit pipeline/`-Connection` value first, then the default.

```powershell
Connect-HomeAssistant -Uri 'https://home.example.net' `
    -AccessToken $token -Name Home | Out-Null

Get-HomeAssistantInfo
Get-HomeAssistantConnection
Disconnect-HomeAssistant
```

Defaults are isolated by runspace. Background jobs and parallel runspaces do
not inherit a connection. A new default disposes the previous default. For
multiple homes, retain explicit connections and use `-NoDefault`:

```powershell
$lab = Connect-HomeAssistant -Uri 'https://lab.example.net' `
    -AccessToken $labToken -Name Lab -NoDefault
$lab | Get-HomeAssistantEntity -Domain light |
    Set-HomeAssistantLight -Power Off
$lab | Disconnect-HomeAssistant
```

Discovery entities retain the connection that produced them. Typed-control
pipeline input uses that connection even when it is not the runspace default,
and rejects a mismatched explicit `-Connection` or a mixed-home batch.

For an application-owned OAuth lifecycle, pass an
`IHomeAssistantAccessTokenProvider` with `-AccessTokenProvider`.

## Discovery workflow

Start with the house, then narrow to a room, device, or entity:

```powershell
Get-HomeAssistantFloor
Get-HomeAssistantArea -Floor 'Ground Floor'
Get-HomeAssistantDevice -Area Kitchen
Get-HomeAssistantEntity -Area Kitchen
Get-HomeAssistantEntity -Area Kitchen -Domain light
```

An area is Home Assistant's physical room/location object; `-Room` is an alias.
Joined entities expose friendly/native names, live state and attributes, device,
effective area, floor, registry metadata, and raw source objects. Effective area
uses the entity's direct assignment first and its device's area otherwise.

Friendly names and native IDs are both accepted. Exact ambiguity raises an
error listing candidate IDs. Non-administrator users can still read entities;
if configuration-entry enrichment is denied, integration details are empty and
the registry snapshot exposes `IsConfigEntryEnrichmentAvailable = false`.

## Action discovery and typed controls

Home Assistant supplies the action catalog at runtime, including custom
integration fields:

```powershell
Get-HomeAssistantAction -Entity 'Kitchen light'
(Get-HomeAssistantAction -Domain light -Action turn_on).Fields
```

Common domains have typed task-level commands:

```powershell
Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -WhatIf
Set-HomeAssistantSwitch -Device 'Coffee machine' -Power Off
Set-HomeAssistantClimate -Entity climate.downstairs -Temperature 21.5 -HvacMode heat
Set-HomeAssistantCover -Entity cover.kitchen -PositionPercent 60
Set-HomeAssistantMediaPlayer -Area LivingRoom -VolumePercent 30 -Playback Play
Set-HomeAssistantMediaPlayer -Entity media_player.kitchen -SoundMode Movie -Shuffle:$false
Invoke-HomeAssistantRemote -Entity remote.harmony -Action TurnOn -Activity 'Watch TV'
Invoke-HomeAssistantRemote -Entity remote.living_room -Action SendCommand -Command Power -RepeatCount 2 -WhatIf
Set-HomeAssistantLock -Entity lock.front_door -Action Unlock -WhatIf
```

Each typed command has target parameter sets for entity, device, area, floor,
label, and joined entity pipeline input. It validates common values and uses
`ShouldProcess`. Lock operations use high confirmation impact.

Climate target ranges require both low and high values and are mutually
exclusive with `-Temperature`. Media-player `-Power Off` and `-Power Toggle`
are standalone operations; use `-Power On` or omit `-Power` when applying
playback, source, sound mode, shuffle, repeat, mute, volume, or content changes
in the same command. Content launch is mutually exclusive with `-Playback` and
`-SeekSeconds`; queue placement and announcement playback are also alternative
Home Assistant modes. `Invoke-HomeAssistantRemote` selects power, send, learn,
or delete through one action parameter and rejects parameters that do not apply
to that action before resolving a target or entering `ShouldProcess`.
`-ColorTemperatureKelvin` and `-RgbColor` are also mutually exclusive.

`Invoke-HomeAssistantAction` remains the extensible path for a custom action or
field that does not belong in a common typed command:

```powershell
Invoke-HomeAssistantAction vacuum send_command `
    -EntityId vacuum.downstairs `
    -Data @{ command = 'clean_spot'; params = @{ repeats = 2 } } `
    -WhatIf
```

`-Service` is accepted as an alias for `-Action`.

## Command map

| Task | Commands |
| --- | --- |
| Connect | `Connect-HomeAssistant`, `Get-HomeAssistantConnection`, `Disconnect-HomeAssistant` |
| Discover the house | `Get-HomeAssistantFloor`, `Get-HomeAssistantArea`, `Get-HomeAssistantDevice`, `Get-HomeAssistantEntity` |
| Discover and invoke actions | `Get-HomeAssistantAction`, `Invoke-HomeAssistantAction` |
| Typed everyday controls | `Set-HomeAssistantLight`, `Set-HomeAssistantSwitch`, `Set-HomeAssistantClimate`, `Set-HomeAssistantCover`, `Set-HomeAssistantMediaPlayer`, `Invoke-HomeAssistantRemote`, `Set-HomeAssistantLock` |
| Read current/history/logbook | `Get-HomeAssistantEntity`, `Get-HomeAssistantHistory`, `Get-HomeAssistantLogbook` |
| Energy and Recorder statistics | `Get/Set-HomeAssistantEnergy`, `Get/Set/Remove/Test-HomeAssistantStatistic`, `Invoke-HomeAssistantRecorderMaintenance` |
| Weather | `Get-HomeAssistantWeather`, `Receive-HomeAssistantWeatherForecast` |
| Cameras | `Get/Set-HomeAssistantCamera`, `Export-HomeAssistantCameraSnapshot` |
| Media browsing | `Get-HomeAssistantMedia` |
| Lovelace dashboards | `Get/Set/Remove-HomeAssistantDashboard` |
| Automation runtime and configuration | `Get/Invoke/Set/Remove-HomeAssistantAutomation` |
| Send/read/stream notifications | `Get-HomeAssistantNotification`, `Send-HomeAssistantNotification`, `Remove-HomeAssistantNotification`, `Receive-HomeAssistantNotification` |
| Calendars | `Get-HomeAssistantCalendar`, `Get-HomeAssistantCalendarEvent`, `Set-HomeAssistantCalendarEvent`, `Remove-HomeAssistantCalendarEvent`, `Receive-HomeAssistantCalendarEvent` |
| Labels and scoped categories | `Get/Set/Remove-HomeAssistantLabel`, `Get/Set/Remove-HomeAssistantCategory` |
| Receive general events | `Receive-HomeAssistantEvent` |
| Inspect the installation | `Get-HomeAssistantInfo` |
| Inspect logs and Repairs | `Get-HomeAssistantLog`, `Get-HomeAssistantIssue` |
| Troubleshoot | `Get-HomeAssistantTrace`, `Export-HomeAssistantDiagnostic`, `Test-HomeAssistantConfiguration`, `Get-HomeAssistantIntegration` |
| Inspect Supervisor | `Get-HomeAssistantApp`, `Get-HomeAssistantBackup`, `Get-HomeAssistantJob` |
| Update and operate apps | `Get-HomeAssistantUpdate`, `Install-HomeAssistantUpdate`, `Invoke-HomeAssistantApp` |
| Back up or restart | `New-HomeAssistantBackup`, `Restart-HomeAssistant` |

Use `Get-Help <command> -Full` for generated parameter, input, output, and
example details.

## Events and automation

`Receive-HomeAssistantEvent` uses a WebSocket subscription and emits events as
they arrive:

```powershell
Receive-HomeAssistantEvent -EventType call_service |
    Where-Object Origin -EQ LOCAL

$nextDoorChange = Receive-HomeAssistantEvent `
    -EntityId binary_sensor.front_door -Count 1 -TimeoutSeconds 60
```

Open-ended streams run until canceled. `-Count` and `-TimeoutSeconds` provide a
bounded wait. Subscription cleanup remains bounded when a command is stopped.

Persistent notifications and calendar event lists have dedicated live feeds:

```powershell
Send-HomeAssistantNotification -Persistent -Message 'Garage is open' -Title Security
Send-HomeAssistantNotification -Area Kitchen -Message 'Dinner is ready' -WhatIf
Receive-HomeAssistantNotification -Count 1

Get-HomeAssistantCalendarEvent -EntityId calendar.home -EndTime (Get-Date).AddDays(7)
Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Dinner `
    -StartTime '2026-08-27T18:00:00+02:00' `
    -EndTime '2026-08-27T20:00:00+02:00' -WhatIf
Receive-HomeAssistantCalendarEvent -EntityId calendar.home -Count 1

Get-HomeAssistantStatistic -Kind Sum
Get-HomeAssistantStatistic -StatisticId sensor.grid_energy `
    -StartTime (Get-Date).AddDays(-1) -Period Hour -Type Change, Sum
Get-HomeAssistantWeather weather.home -Forecast -ForecastType Daily
Receive-HomeAssistantWeatherForecast weather.home -ForecastType Hourly -Count 1

Get-HomeAssistantCamera camera.front -Stream
Export-HomeAssistantCameraSnapshot camera.front ./front.jpg -Width 1280 -Height 720
Get-HomeAssistantMedia -PlayerEntityId media_player.kitchen -Search dinner
Get-HomeAssistantDashboard -Configuration -UrlPath house-main
Invoke-HomeAssistantAutomation automation.morning -WhatIf
Get-HomeAssistantAutomation morning-routine -Configuration
```

Timed and all-day calendar parameter sets prevent mixed boundary types.
Supplying `-Uid` selects update behavior. Label/category setters similarly use
create/update parameter sets, while `-ClearColor`, `-ClearDescription`, and
`-ClearIcon` intentionally clear nullable registry fields.

Camera snapshot export writes atomically and never exposes the camera access
token. Lovelace configuration and resource mutations require administrator
permissions and storage-backed data. Automation runtime invocation and editable
definitions use separate commands and separate identifiers, preventing a run
operation from silently becoming a configuration rewrite.

## Safety and Supervisor boundaries

Mutation cmdlets implement `SupportsShouldProcess`; use `-WhatIf` in discovery
and deployment scripts. A custom integration can define semantics that the
generic action command cannot infer, so callers still own allowlists and
authorization policy.

Supervisor commands use Home Assistant Core's administrator-only
`supervisor/api` WebSocket proxy and `/api/hassio` log proxy. They require Home
Assistant OS or a supervised installation and suitable permissions.

Destructive restore, wipe, recovery, host shutdown, and arbitrary package
installation do not have convenience cmdlets. Advanced .NET callers can use the
bounded raw Supervisor surface when they deliberately own those risks.

HomeAssistantX does not log or persist tokens, passwords, logs, diagnostics, or
house inventory.
