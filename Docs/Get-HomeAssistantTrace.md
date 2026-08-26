---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantTrace
## SYNOPSIS
Gets automation or script trace summaries, or one complete trace run.

## SYNTAX
### List (Default)
```powershell
Get-HomeAssistantTrace [-Domain] <string> [-ItemId] <string> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Run
```powershell
Get-HomeAssistantTrace [-Domain] <string> [-ItemId] <string> -RunId <string> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Gets automation or script trace summaries, or one complete trace run.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantTrace -Domain automation -ItemId 'morning_lights'
```

Returns trace summaries; add RunId to retrieve one complete run.

## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: List, Run
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Domain
Trace domain: automation or script.

```yaml
Type: String
Parameter Sets: List, Run
Aliases: None
Possible values: automation, script

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ItemId
Automation or script item identifier.

```yaml
Type: String
Parameter Sets: List, Run
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RunId
Exact trace run identifier. Omit it to list trace summaries.

```yaml
Type: String
Parameter Sets: Run
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

- `HomeAssistantX.Operations.HomeAssistantTraceSummary`
- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
