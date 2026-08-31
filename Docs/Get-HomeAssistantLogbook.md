---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantLogbook
## SYNOPSIS
Reads human-oriented Recorder logbook activity for a bounded time range.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantLogbook [[-EntityId] <string>] [-StartTime <DateTimeOffset>] [-EndTime <DateTimeOffset>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads human-oriented Recorder logbook activity for a bounded time range.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantLogbook -StartTime (Get-Date).Date -EntityId light.kitchen
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

### -EndTime
Specifies a value for end time.

```yaml
Type: DateTimeOffset
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Specifies a value for entity id.

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

### -StartTime
Specifies a value for start time.

```yaml
Type: DateTimeOffset
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

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Rest.HomeAssistantLogbookEntry`

## RELATED LINKS

- None
