---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Find-HomeAssistant
## SYNOPSIS
Finds Home Assistant instances advertised on the local IPv4 network.

## SYNTAX
### __AllParameterSets
```powershell
Find-HomeAssistant [-TimeoutSeconds <int>] [<CommonParameters>]
```

## DESCRIPTION
Finds Home Assistant instances advertised on the local IPv4 network.

## EXAMPLES

### EXAMPLE 1
```powershell
Find-HomeAssistant -TimeoutSeconds 5
```


## PARAMETERS

### -TimeoutSeconds
Maximum time to listen for local advertisements, from 1 through 60 seconds.

```yaml
Type: Int32
Parameter Sets: __AllParameterSets
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

- `None`

## OUTPUTS

- `HomeAssistantX.Discovery.HomeAssistantDiscoveredInstance`

## RELATED LINKS

- None
