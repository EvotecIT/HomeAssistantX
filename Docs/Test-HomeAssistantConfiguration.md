---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Test-HomeAssistantConfiguration
## SYNOPSIS
Validates the active Home Assistant configuration without restarting Core.

## SYNTAX
### __AllParameterSets
```powershell
Test-HomeAssistantConfiguration [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Validates the active Home Assistant configuration without restarting Core.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Test-HomeAssistantConfiguration
```

Returns Home Assistant's validation result without restarting Core.

## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Rest.HomeAssistantConfigurationCheck`

## RELATED LINKS

- None
