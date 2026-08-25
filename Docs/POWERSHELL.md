# HomeAssistantX PowerShell

The HomeAssistantX module is a thin, task-oriented shell over the same .NET
client used by applications. Windows PowerShell 5.1 loads the `net472` binary.
PowerShell 7 uses the portable `netstandard2.0` payload on Windows, macOS, and
Linux; a `net10.0` build is also validated for current hosts and direct binary
consumers.

## Connection model

`Connect-HomeAssistant` validates both REST and WebSocket access and returns an
explicit `HomeAssistantConnection`. Every other command requires that object
through `-Connection` or the pipeline. The module does not keep a process-wide
default connection.

```powershell
$token = Get-Secret HomeAssistantToken -AsPlainText
$home = Connect-HomeAssistant -Uri 'https://home.example.net' `
    -AccessToken $token -Name 'Home'

$home | Get-HomeAssistantInfo
$home | Disconnect-HomeAssistant
```

For an application-owned OAuth lifecycle, pass an
`IHomeAssistantAccessTokenProvider` with `-AccessTokenProvider` instead of a
token string.

## Command design

The module groups commands by operator task. Parameter sets select a target or
data source; they do not multiply the command count for every integration,
domain, app, or update type.

| Task | Command | Important parameter sets |
| --- | --- | --- |
| Connect and disconnect | `Connect-HomeAssistant`, `Disconnect-HomeAssistant` | `Token`, `Provider` |
| Inspect the installation | `Get-HomeAssistantInfo` | `Overview`, `Capabilities`, `Health`, `Supervisor` |
| Read current and historical state | `Get-HomeAssistantEntity`, `Get-HomeAssistantHistory` | entity, domain, all |
| Receive notifications | `Receive-HomeAssistantEvent` | `Event`, `Entity`, `All` |
| Invoke any Core action | `Invoke-HomeAssistantAction` | `Data`, `Entity`, `Device`, `Area`, `Floor`, `Label` |
| Inspect logs | `Get-HomeAssistantLog` | `SystemLog`, `Legacy`, `Core`, `Supervisor`, `Host`, `App` |
| Troubleshoot Core | `Get-HomeAssistantIssue`, `Get-HomeAssistantTrace`, `Export-HomeAssistantDiagnostic`, `Test-HomeAssistantConfiguration` | Core Repairs, trace list/run, config-entry/device diagnostic |
| Inspect integrations | `Get-HomeAssistantIntegration` | all, id, domain |
| Inspect Supervisor | `Get-HomeAssistantApp`, `Get-HomeAssistantBackup`, `Get-HomeAssistantJob` | optional exact identifiers |
| Discover and install updates | `Get-HomeAssistantUpdate`, `Install-HomeAssistantUpdate` | entity, Core, Supervisor, OS, app |
| Operate apps | `Invoke-HomeAssistantApp` | one `Action` enum instead of six lifecycle cmdlets |
| Back up or restart | `New-HomeAssistantBackup`, `Restart-HomeAssistant` | Core, Supervisor, host, app, integration |

Use `Get-Help <command> -Full` for generated parameter and output details.

## Generic actions

Home Assistant's domain/action catalog is extensible, so the module does not
attempt to generate hundreds of commands such as `Set-Light` or
`Open-Cover`. `Invoke-HomeAssistantAction` keeps the native model visible and
provides mutually exclusive target parameter sets:

```powershell
$home | Invoke-HomeAssistantAction -Domain climate -Action set_temperature `
    -EntityId climate.downstairs -Data @{ temperature = 21.5 } -WhatIf

$home | Invoke-HomeAssistantAction light turn_off -FloorId ground_floor
```

`-Service` is accepted as an alias for `-Action` for users familiar with the
older Home Assistant terminology.

## Events and automation

`Receive-HomeAssistantEvent` uses a WebSocket subscription and emits events as
they arrive. It runs until canceled or until the connection fails after its
configured reconnect policy.

```powershell
$home | Receive-HomeAssistantEvent -EventType call_service |
    Where-Object { $_.Origin -eq 'LOCAL' }

$nextDoorChange = $home | Receive-HomeAssistantEvent `
    -EntityId binary_sensor.front_door -Count 1 -TimeoutSeconds 60
```

For unattended automation, catch the typed HomeAssistantX exceptions and let
the host decide whether authentication failures require token refresh or user
reauthorization.

## Safety boundaries

The following commands implement `SupportsShouldProcess`:

- `Export-HomeAssistantDiagnostic`
- `Install-HomeAssistantUpdate`
- `Invoke-HomeAssistantAction`
- `Invoke-HomeAssistantApp`
- `New-HomeAssistantBackup`
- `Restart-HomeAssistant`

Use `-WhatIf` in discovery and deployment scripts before allowing a mutation.
High-impact Supervisor operations prompt according to PowerShell's confirmation
preference. A generic Home Assistant action cannot infer whether a custom
integration considers an operation dangerous, so callers remain responsible
for their own allowlists and confirmation policy.

`New-HomeAssistantBackup -Password` accepts a `SecureString`. HomeAssistantX
does not log or persist tokens, passwords, logs, or diagnostic payloads.

## Supervisor availability

The normal connection uses Home Assistant Core's administrator-only
`supervisor/api` WebSocket proxy and the authenticated `/api/hassio` log proxy.
This keeps one explicit connection for normal operator scripts. Supervisor
commands require Home Assistant OS or a supervised installation and sufficient
permissions; they do not work on Container or Core-only installations.

The .NET API also exposes a separate direct Supervisor bearer-token client for
code running in trusted local app/add-on contexts. The PowerShell module does
not ask users to mix that credential with the Core connection.

Routine inventory, logs, jobs, backups, updates, restarts, and app lifecycle
are modeled. Destructive restore, wipe, recovery, host shutdown, and arbitrary
package installation do not receive convenience cmdlets. Advanced .NET callers
can use the bounded raw Supervisor surface when they deliberately own those
risks.
