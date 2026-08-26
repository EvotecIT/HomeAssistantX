---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantLog
## SYNOPSIS
Gets structured system-log entries or bounded Core, Supervisor, host, and app log lines.

## SYNTAX
### SystemLog (Default)
```powershell
Get-HomeAssistantLog -Connection <HomeAssistantConnection> [-SystemLog] [<CommonParameters>]
```

### Legacy
```powershell
Get-HomeAssistantLog -LegacyErrorLog -Connection <HomeAssistantConnection> [<CommonParameters>]
```

### Core
```powershell
Get-HomeAssistantLog -Core -Connection <HomeAssistantConnection> [-Tail <int>] [<CommonParameters>]
```

### Supervisor
```powershell
Get-HomeAssistantLog -Supervisor -Connection <HomeAssistantConnection> [-Tail <int>] [<CommonParameters>]
```

### Host
```powershell
Get-HomeAssistantLog -HostSystem -Connection <HomeAssistantConnection> [-Tail <int>] [<CommonParameters>]
```

### App
```powershell
Get-HomeAssistantLog -App <string> -Connection <HomeAssistantConnection> [-Tail <int>] [<CommonParameters>]
```

## DESCRIPTION
Gets structured system-log entries or bounded Core, Supervisor, host, and app log lines.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantLog -Core -Tail 200
```

Returns bounded plaintext log lines through the authenticated Supervisor proxy.

## PARAMETERS

### -App
Supervisor app/add-on slug whose logs should be returned.

```yaml
Type: String
Parameter Sets: App
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: SystemLog, Legacy, Core, Supervisor, Host, App
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Core
Returns bounded Home Assistant Core container logs through Supervisor.

```yaml
Type: SwitchParameter
Parameter Sets: Core
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HostSystem
Returns bounded host-system logs.

```yaml
Type: SwitchParameter
Parameter Sets: Host
Aliases: Host
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LegacyErrorLog
Returns the legacy plaintext Core error log when that endpoint is enabled.

```yaml
Type: SwitchParameter
Parameter Sets: Legacy
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Returns bounded Supervisor logs.

```yaml
Type: SwitchParameter
Parameter Sets: Supervisor
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SystemLog
Returns structured Core system-log entries. This is the default source.

```yaml
Type: SwitchParameter
Parameter Sets: SystemLog
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Tail
Maximum number of trailing plaintext log lines, from 1 through 10000.

```yaml
Type: Int32
Parameter Sets: Core, Supervisor, Host, App
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Operations.HomeAssistantSystemLogEntry`
- `HomeAssistantX.PowerShell.HomeAssistantLogLine`

## RELATED LINKS

- None
