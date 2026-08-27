---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantStatistic
## SYNOPSIS
Lists Recorder statistics or returns typed aggregated values.

## SYNTAX
### Catalog (Default)
```powershell
Get-HomeAssistantStatistic [-Kind <HomeAssistantStatisticKind>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Metadata
```powershell
Get-HomeAssistantStatistic -Metadata [-StatisticId <string[]>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Values
```powershell
Get-HomeAssistantStatistic [-StatisticId] <string[]> -StartTime <DateTimeOffset> -Period <HomeAssistantStatisticPeriod> [-EndTime <DateTimeOffset>] [-Type <HomeAssistantStatisticType[]>] [-Unit <Dictionary[string,string]>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Lists Recorder statistics or returns typed aggregated values.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantStatistic -Kind Sum
```


### EXAMPLE 2
```powershell
Get-HomeAssistantStatistic -StatisticId sensor.grid_energy -StartTime (Get-Date).AddDays(-1) -Period Hour -Type Change,Sum
```


## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Catalog, Metadata, Values
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
Parameter Sets: Values
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Kind
Specifies a value for kind.

```yaml
Type: HomeAssistantStatisticKind
Parameter Sets: Catalog
Aliases: None
Possible values: Any, Mean, Sum

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Metadata
Specifies the metadata switch.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Period
Specifies a value for period.

```yaml
Type: HomeAssistantStatisticPeriod
Parameter Sets: Values
Aliases: None
Possible values: FiveMinute, Hour, Day, Week, Month

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Specifies a value for start time.

```yaml
Type: DateTimeOffset
Parameter Sets: Values
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StatisticId
Filters metadata when used with -Metadata and is required when requesting aggregated values.

```yaml
Type: String[]
Parameter Sets: Metadata, Values
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Type
Specifies one or more values for type.

```yaml
Type: HomeAssistantStatisticType[]
Parameter Sets: Values
Aliases: None
Possible values: Change, LastReset, Maximum, Mean, Minimum, State, Sum

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Unit
Specifies one or more values for unit.

```yaml
Type: Dictionary`2
Parameter Sets: Values
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

- `HomeAssistantX.Recorder.HomeAssistantStatisticMetadata`
- `HomeAssistantX.Recorder.HomeAssistantStatisticSeries`

## RELATED LINKS

- None
