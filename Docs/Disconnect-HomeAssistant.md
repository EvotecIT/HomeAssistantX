---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Disconnect-HomeAssistant
## SYNOPSIS
Closes and disposes an explicit Home Assistant connection.

## SYNTAX
### __AllParameterSets
```powershell
Disconnect-HomeAssistant -Connection <HomeAssistantConnection> [<CommonParameters>]
```

## DESCRIPTION
Closes and disposes an explicit Home Assistant connection.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Disconnect-HomeAssistant
```

Closes both transports and disposes the connection.

## PARAMETERS

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
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

- `None`

## RELATED LINKS

- None
