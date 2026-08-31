---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Receive-HomeAssistantWeatherForecast
## SYNOPSIS
Streams weather forecast updates without polling.

## SYNTAX
### __AllParameterSets
```powershell
Receive-HomeAssistantWeatherForecast [-EntityId] <string> -ForecastType <HomeAssistantWeatherForecastType> [-Count <Int32>] [-TimeoutSeconds <Int32>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Streams weather forecast updates without polling.

## EXAMPLES

### EXAMPLE 1
```powershell
Receive-HomeAssistantWeatherForecast weather.home -ForecastType Hourly -Count 1 -TimeoutSeconds 30
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

### -Count
Specifies a value for count.

```yaml
Type: Int32
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

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ForecastType
Specifies a value for forecast type.

```yaml
Type: HomeAssistantWeatherForecastType
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: Daily, Hourly, TwiceDaily

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds
Specifies a value for timeout seconds.

```yaml
Type: Int32
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

- `HomeAssistantX.Weather.HomeAssistantWeatherForecastUpdate`

## RELATED LINKS

- None
