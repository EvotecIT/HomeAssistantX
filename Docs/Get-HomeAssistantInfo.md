---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantInfo
## SYNOPSIS
Gets Core configuration, discovered capabilities, system health, or Supervisor information.

## SYNTAX
### Overview (Default)
```powershell
Get-HomeAssistantInfo [-Overview] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Capabilities
```powershell
Get-HomeAssistantInfo -Capabilities [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Health
```powershell
Get-HomeAssistantInfo -Health [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Supervisor
```powershell
Get-HomeAssistantInfo -Supervisor [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Gets Core configuration, discovered capabilities, system health, or Supervisor information.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantInfo -Capabilities
```

Reports installed and permission-dependent operational capabilities without changing Home Assistant.

## PARAMETERS

### -Capabilities
Returns discovered operational capabilities and their availability.

```yaml
Type: SwitchParameter
Parameter Sets: Capabilities
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Overview, Capabilities, Health, Supervisor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Health
Returns the streamed Core system-health snapshot.

```yaml
Type: SwitchParameter
Parameter Sets: Health
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Overview
Returns Core configuration and version information. This is the default view.

```yaml
Type: SwitchParameter
Parameter Sets: Overview
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Returns Supervisor and Home Assistant OS information when available.

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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Models.HomeAssistantConfiguration`
- `HomeAssistantX.Operations.HomeAssistantCapabilityReport`
- `HomeAssistantX.Operations.HomeAssistantSystemHealthSnapshot`
- `HomeAssistantX.Supervisor.HomeAssistantSupervisorOverview`

## RELATED LINKS

- None
