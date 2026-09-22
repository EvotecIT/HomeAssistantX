---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Test-HomeAssistantAutomationDraft
## SYNOPSIS
Asks Home Assistant to validate an automation draft without saving it.

## SYNTAX
### __AllParameterSets
```powershell
Test-HomeAssistantAutomationDraft [-ConfigurationJson] <string> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Asks Home Assistant to validate an automation draft without saving it.

## EXAMPLES

### EXAMPLE 1
```powershell
Test-HomeAssistantAutomationDraft -ConfigurationJson $automationJson
```


## PARAMETERS

### -ConfigurationJson
Specifies a value for configuration json.

```yaml
Type: String
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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
