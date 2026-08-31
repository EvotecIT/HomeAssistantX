---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantLabel
## SYNOPSIS
Lists Home Assistant labels, optionally selecting one by name or ID.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantLabel [[-Label] <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Lists Home Assistant labels, optionally selecting one by name or ID.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantLabel
```


### EXAMPLE 2
```powershell
Get-HomeAssistantLabel -Label Security
```


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

### -Label
Optional label name or native ID.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
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

- `HomeAssistantX.Registries.HomeAssistantLabel`

## RELATED LINKS

- None
