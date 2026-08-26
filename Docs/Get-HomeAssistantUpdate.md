---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantUpdate
## SYNOPSIS
Gets update entities or Supervisor component and app updates.

## SYNTAX
### Entity (Default)
```powershell
Get-HomeAssistantUpdate -Connection <HomeAssistantConnection> [-Entity] [-AvailableOnly] [<CommonParameters>]
```

### Supervisor
```powershell
Get-HomeAssistantUpdate -Supervisor -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Gets update entities or Supervisor component and app updates.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantUpdate -AvailableOnly
```

Returns update entities that currently advertise a newer version.

## PARAMETERS

### -AvailableOnly
Limits entity results to updates that are currently available.

```yaml
Type: SwitchParameter
Parameter Sets: Entity
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Entity, Supervisor
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Entity
Returns Home Assistant update entities. This is the default source.

```yaml
Type: SwitchParameter
Parameter Sets: Entity
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Supervisor
Returns Supervisor component and app updates.

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

- `HomeAssistantX.Operations.HomeAssistantUpdate`
- `HomeAssistantX.Supervisor.HomeAssistantSupervisorUpdate`

## RELATED LINKS

- None
