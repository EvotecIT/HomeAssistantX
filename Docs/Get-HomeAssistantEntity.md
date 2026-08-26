---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantEntity
## SYNOPSIS
Gets current entity states by identifier, domain, or all entities.

## SYNTAX
### All (Default)
```powershell
Get-HomeAssistantEntity -Connection <HomeAssistantConnection> [-All] [<CommonParameters>]
```

### Entity
```powershell
Get-HomeAssistantEntity [-EntityId] <string[]> -Connection <HomeAssistantConnection> [<CommonParameters>]
```

### Domain
```powershell
Get-HomeAssistantEntity -Domain <string> -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Gets current entity states by identifier, domain, or all entities.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantEntity -Domain light
```

Filters the current state snapshot to the light domain.

## PARAMETERS

### -All
Returns all current entity states. This is the default behavior.

```yaml
Type: SwitchParameter
Parameter Sets: All
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
Parameter Sets: All, Entity, Domain
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Domain
Entity domain to filter, such as light or sensor.

```yaml
Type: String
Parameter Sets: Domain
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
One or more exact entity identifiers.

```yaml
Type: String[]
Parameter Sets: Entity
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Models.HomeAssistantState`

## RELATED LINKS

- None
