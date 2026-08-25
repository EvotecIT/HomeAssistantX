---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantHistory
## SYNOPSIS
Gets recorder history for one or more entity identifiers.

## SYNTAX
### __AllParameterSets
```powershell
Get-HomeAssistantHistory [-EntityId] <string[]> -Connection <HomeAssistantConnection> [-StartTime <DateTimeOffset>] [-EndTime <DateTimeOffset>] [-MinimalResponse] [-NoAttributes] [-SignificantChangesOnly] [<CommonParameters>]
```

## DESCRIPTION
Gets recorder history for one or more entity identifiers.

## EXAMPLES

### EXAMPLE 1
```powershell
$ha | Get-HomeAssistantHistory -EntityId 'sensor.temperature' -StartTime (Get-Date).AddHours(-1) -MinimalResponse
```

Returns recorder history without polling the current state endpoint.

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

### -EndTime
Exclusive history end time.

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
Entity identifiers whose state history should be returned.

```yaml
Type: String[]
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -MinimalResponse
Requests Home Assistant's compact history representation.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NoAttributes
Omits state attributes from the history response.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SignificantChangesOnly
Requests only significant state changes where supported.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Inclusive history start time. Defaults to Home Assistant's endpoint behavior.

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

- `HomeAssistantX.Models.HomeAssistantState`

## RELATED LINKS

- None
