---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantIntegration
## SYNOPSIS
Gets Home Assistant configuration entries by identifier, domain, or all integrations.

## SYNTAX
### All (Default)
```powershell
Get-HomeAssistantIntegration [-All] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Id
```powershell
Get-HomeAssistantIntegration [-EntryId] <string> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Domain
```powershell
Get-HomeAssistantIntegration -Domain <string> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Gets Home Assistant configuration entries by identifier, domain, or all integrations.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantIntegration -Domain mqtt
```

Returns configuration entries belonging to one integration domain.

## PARAMETERS

### -All
Returns all configuration entries. This is the default behavior.

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
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: All, Id, Domain
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Domain
Integration domain, such as hue or mqtt.

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

### -EntryId
Exact configuration-entry identifier.

```yaml
Type: String
Parameter Sets: Id
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

- `HomeAssistantX.Registries.HomeAssistantConfigEntry`

## RELATED LINKS

- None
