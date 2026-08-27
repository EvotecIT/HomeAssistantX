---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantWeather
## SYNOPSIS
Reads current observations, forecasts, or supported weather units.

## SYNTAX
### Current (Default)
```powershell
Get-HomeAssistantWeather [[-EntityId] <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Forecast
```powershell
Get-HomeAssistantWeather [-EntityId] <string> -Forecast -ForecastType <HomeAssistantWeatherForecastType> [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Units
```powershell
Get-HomeAssistantWeather -ConvertibleUnits [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads current observations, forecasts, or supported weather units.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantWeather
```


### EXAMPLE 2
```powershell
Get-HomeAssistantWeather -EntityId weather.home -Forecast -ForecastType Daily
```


## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Current, Forecast, Units
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ConvertibleUnits
Specifies the convertible units switch.

```yaml
Type: SwitchParameter
Parameter Sets: Units
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Filters current observations when provided and is required when requesting a forecast.

```yaml
Type: String
Parameter Sets: Current, Forecast
Aliases: None
Possible values:

Required: False
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Forecast
Specifies the forecast switch.

```yaml
Type: SwitchParameter
Parameter Sets: Forecast
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ForecastType
Specifies a value for forecast type.

```yaml
Type: HomeAssistantWeatherForecastType
Parameter Sets: Forecast
Aliases: None
Possible values: Daily, Hourly, TwiceDaily

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

- `HomeAssistantX.Weather.HomeAssistantWeatherObservation`
- `HomeAssistantX.Weather.HomeAssistantWeatherForecastUpdate`
- `System.Collections.Generic.IReadOnlyDictionary[System.String,System.Collections.Generic.IReadOnlyList[System.String]]`

## RELATED LINKS

- None
