---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Test-HomeAssistantStatistic
## SYNOPSIS
Validates Recorder long-term statistics and returns every issue reported by Home Assistant.

## SYNTAX
### __AllParameterSets
```powershell
Test-HomeAssistantStatistic [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Validates Recorder long-term statistics and returns every issue reported by Home Assistant.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HomeAssistantStatistic
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

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
