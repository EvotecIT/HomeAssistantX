---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantStatistic
## SYNOPSIS
Updates metadata, converts units, adjusts sums, or imports Recorder statistics.

## SYNTAX
### Metadata
```powershell
Set-HomeAssistantStatistic [-StatisticId] <string> [-UnitClass <string>] [-ClearUnitClass] [-UnitOfMeasurement <string>] [-ClearUnitOfMeasurement] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Unit
```powershell
Set-HomeAssistantStatistic [-StatisticId] <string> -ChangeUnit [-OldUnit <string>] [-NewUnit <string>] [-ClearOldUnit] [-ClearNewUnit] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### AdjustSum
```powershell
Set-HomeAssistantStatistic [-StatisticId] <string> -AdjustSum <double> -StartTime <DateTimeOffset> [-Unit <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Import
```powershell
Set-HomeAssistantStatistic -ImportMetadata <HomeAssistantStatisticImportMetadata> -ImportRow <HomeAssistantStatisticImportRow[]> [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Updates metadata, converts units, adjusts sums, or imports Recorder statistics.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 1.25 -StartTime (Get-Date) -Unit kWh -WhatIf
```


## PARAMETERS

### -AdjustSum
Specifies a value for adjust sum.

```yaml
Type: Double
Parameter Sets: AdjustSum
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ChangeUnit
Specifies the change unit switch.

```yaml
Type: SwitchParameter
Parameter Sets: Unit
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearNewUnit
Specifies the clear new unit switch.

```yaml
Type: SwitchParameter
Parameter Sets: Unit
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearOldUnit
Specifies the clear old unit switch.

```yaml
Type: SwitchParameter
Parameter Sets: Unit
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearUnitClass
Specifies the clear unit class switch.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearUnitOfMeasurement
Specifies the clear unit of measurement switch.

```yaml
Type: SwitchParameter
Parameter Sets: Metadata
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Metadata, Unit, AdjustSum, Import
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ImportMetadata
Specifies a value for import metadata.

```yaml
Type: HomeAssistantStatisticImportMetadata
Parameter Sets: Import
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ImportRow
Specifies one or more values for import row.

```yaml
Type: HomeAssistantStatisticImportRow[]
Parameter Sets: Import
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -NewUnit
Specifies a value for new unit.

```yaml
Type: String
Parameter Sets: Unit
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OldUnit
Specifies a value for old unit.

```yaml
Type: String
Parameter Sets: Unit
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Specifies a value for start time.

```yaml
Type: DateTimeOffset
Parameter Sets: AdjustSum
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StatisticId
Specifies a value for statistic id.

```yaml
Type: String
Parameter Sets: Metadata, Unit, AdjustSum
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Unit
Specifies a value for unit.

```yaml
Type: String
Parameter Sets: AdjustSum
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UnitClass
Specifies a value for unit class.

```yaml
Type: String
Parameter Sets: Metadata
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UnitOfMeasurement
Specifies a value for unit of measurement.

```yaml
Type: String
Parameter Sets: Metadata
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

- `HomeAssistantX.Recorder.HomeAssistantStatisticImportRow[]`
- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `None`

## RELATED LINKS

- None
