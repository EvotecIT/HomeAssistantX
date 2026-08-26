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
$lab | Get-HomeAssistantEntity
$lab | Disconnect-HomeAssistant
```

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
error listing candidate IDs.

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
Set-HomeAssistantLock -Entity lock.front_door -Action Unlock -WhatIf
```

Each typed command has target parameter sets for entity, device, area, floor,
and joined entity pipeline input. It validates common values and uses
`ShouldProcess`. Lock operations use high confirmation impact.

Climate target ranges require both low and high values and are mutually
exclusive with `-Temperature`. Media-player `-Power Off` and `-Power Toggle`
are standalone operations; use `-Power On` or omit `-Power` when applying
playback, source, mute, volume, or content changes in the same command. Content
launch and `-Playback` are mutually exclusive because both start playback.

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
| Typed everyday controls | `Set-HomeAssistantLight`, `Set-HomeAssistantSwitch`, `Set-HomeAssistantClimate`, `Set-HomeAssistantCover`, `Set-HomeAssistantMediaPlayer`, `Set-HomeAssistantLock` |
| Read current/history | `Get-HomeAssistantEntity`, `Get-HomeAssistantHistory` |
| Receive notifications | `Receive-HomeAssistantEvent` |
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
