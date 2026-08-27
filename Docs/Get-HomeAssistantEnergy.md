---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantEnergy
## SYNOPSIS
Reads Energy preferences, capabilities, validation, provider forecasts, or fossil-energy periods.

## SYNTAX
### Preferences (Default)
```powershell
Get-HomeAssistantEnergy [-Preferences] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Info
```powershell
Get-HomeAssistantEnergy -Info [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Validation
```powershell
Get-HomeAssistantEnergy -Validation [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### SolarForecast
```powershell
Get-HomeAssistantEnergy -SolarForecast [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### FossilConsumption
```powershell
Get-HomeAssistantEnergy -FossilConsumption -StartTime <DateTimeOffset> -EndTime <DateTimeOffset> -EnergyStatisticId <string[]> -Co2StatisticId <string> -Period <HomeAssistantEnergyPeriod> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads Energy preferences, capabilities, validation, provider forecasts, or fossil-energy periods.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantEnergy
```


### EXAMPLE 2
```powershell
Get-HomeAssistantEnergy -FossilConsumption -StartTime (Get-Date).AddDays(-1) -EndTime (Get-Date) -EnergyStatisticId sensor.grid_energy -Co2StatisticId sensor.co2_intensity -Period Hour
```


## PARAMETERS

### -Co2StatisticId
Specifies a value for co2 statistic id.

```yaml
Type: String
Parameter Sets: FossilConsumption
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Preferences, Info, Validation, SolarForecast, FossilConsumption
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
Parameter Sets: FossilConsumption
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EnergyStatisticId
Specifies one or more values for energy statistic id.

```yaml
Type: String[]
Parameter Sets: FossilConsumption
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -FossilConsumption
Specifies the fossil consumption switch.

```yaml
Type: SwitchParameter
Parameter Sets: FossilConsumption
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Info
Specifies the info switch.

```yaml
Type: SwitchParameter
Parameter Sets: Info
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
Type: HomeAssistantEnergyPeriod
Parameter Sets: FossilConsumption
Aliases: None
Possible values: FiveMinute, Hour, Day, Month

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Preferences
Specifies the preferences switch.

```yaml
Type: SwitchParameter
Parameter Sets: Preferences
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SolarForecast
Specifies the solar forecast switch.

```yaml
Type: SwitchParameter
Parameter Sets: SolarForecast
Aliases: None
Possible values:

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
Parameter Sets: FossilConsumption
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Validation
Specifies the validation switch.

```yaml
Type: SwitchParameter
Parameter Sets: Validation
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

- `HomeAssistantX.Energy.HomeAssistantEnergyPreferences`
- `HomeAssistantX.Energy.HomeAssistantEnergyInfo`
- `System.Text.Json.JsonElement`
- `System.Collections.Generic.IReadOnlyDictionary[System.String,System.Text.Json.JsonElement]`
- `HomeAssistantX.Energy.HomeAssistantFossilEnergyPeriod`

## RELATED LINKS

- None
