---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Receive-HomeAssistantCalendarEvent
## SYNOPSIS
Streams refreshed event lists for one calendar without polling.

## SYNTAX
### __AllParameterSets
```powershell
Receive-HomeAssistantCalendarEvent [-EntityId] <string> [-StartTime <DateTimeOffset>] [-EndTime <DateTimeOffset>] [-Count <Int32>] [-TimeoutSeconds <Int32>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Streams refreshed event lists for one calendar without polling.

## EXAMPLES

### EXAMPLE 1
```powershell
Receive-HomeAssistantCalendarEvent -EntityId calendar.home -Count 1 -TimeoutSeconds 30
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
Stops after emitting this many event-list updates.

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

### -EndTime
Range end. Defaults to 30 days after the start.

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
Calendar entity identifier.

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

### -StartTime
Range start. Defaults to the current instant.

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

### -TimeoutSeconds
Stops normally after this many seconds.

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

- `HomeAssistantX.Calendars.HomeAssistantCalendarEventUpdate`

## RELATED LINKS

- None
