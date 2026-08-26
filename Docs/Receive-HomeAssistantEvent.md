---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Receive-HomeAssistantEvent
## SYNOPSIS
Streams Home Assistant events without polling until canceled.

## SYNTAX
### Event (Default)
```powershell
Receive-HomeAssistantEvent [-EventType] <string> -Connection <HomeAssistantConnection> [-Count <Int32>] [-TimeoutSeconds <Int32>] [<CommonParameters>]
```

### Entity
```powershell
Receive-HomeAssistantEvent [-EntityId] <string[]> -Connection <HomeAssistantConnection> [-Count <Int32>] [-TimeoutSeconds <Int32>] [<CommonParameters>]
```

### All
```powershell
Receive-HomeAssistantEvent -All -Connection <HomeAssistantConnection> [-Count <Int32>] [-TimeoutSeconds <Int32>] [<CommonParameters>]
```

## DESCRIPTION
Streams Home Assistant events without polling until canceled.

## EXAMPLES

### EXAMPLE 1
```powershell
$event = $ha | Receive-HomeAssistantEvent -EntityId 'binary_sensor.front_door' -Count 1 -TimeoutSeconds 60
```

Uses a WebSocket subscription and returns after one matching event or the timeout.

## PARAMETERS

### -All
Streams all Home Assistant event types until the pipeline is stopped.

```yaml
Type: SwitchParameter
Parameter Sets: All
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Event, Entity, All
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Count
Stops after emitting this many matching events. Omit it to keep streaming.

```yaml
Type: Int32
Parameter Sets: Event, Entity, All
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Streams state-change events only for these entity identifiers.

```yaml
Type: String[]
Parameter Sets: Entity
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EventType
Exact Home Assistant event type to stream.

```yaml
Type: String
Parameter Sets: Event
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds
Stops normally when this many seconds elapse without requiring pipeline cancellation.

```yaml
Type: Int32
Parameter Sets: Event, Entity, All
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

- `HomeAssistantX.Models.HomeAssistantEvent`

## RELATED LINKS

- None
