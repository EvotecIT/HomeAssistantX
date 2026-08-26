---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantCalendarEvent
## SYNOPSIS
Creates or updates a timed or all-day Home Assistant calendar event.

## SYNTAX
### CreateTimed (Default)
```powershell
Set-HomeAssistantCalendarEvent [-EntityId] <string> -Summary <string> -StartTime <DateTimeOffset> -EndTime <DateTimeOffset> [-Description <string>] [-Location <string>] [-RecurrenceRule <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### UpdateTimed
```powershell
Set-HomeAssistantCalendarEvent [-EntityId] <string> -Summary <string> -StartTime <DateTimeOffset> -EndTime <DateTimeOffset> -Uid <string> [-RecurrenceId <string>] [-RecurrenceRange <string>] [-Description <string>] [-Location <string>] [-RecurrenceRule <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### CreateAllDay
```powershell
Set-HomeAssistantCalendarEvent [-EntityId] <string> -Summary <string> -StartDate <string> -EndDate <string> [-Description <string>] [-Location <string>] [-RecurrenceRule <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### UpdateAllDay
```powershell
Set-HomeAssistantCalendarEvent [-EntityId] <string> -Summary <string> -StartDate <string> -EndDate <string> -Uid <string> [-RecurrenceId <string>] [-RecurrenceRange <string>] [-Description <string>] [-Location <string>] [-RecurrenceRule <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates or updates a timed or all-day Home Assistant calendar event.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Dinner -StartTime '2026-08-27 18:00' -EndTime '2026-08-27 20:00' -WhatIf
```


### EXAMPLE 2
```powershell
Set-HomeAssistantCalendarEvent -EntityId calendar.home -Uid event-1 -RecurrenceId 20260827 -Summary Dinner -StartDate 2026-08-27 -EndDate 2026-08-28
```


## PARAMETERS

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Description
Optional event description.

```yaml
Type: String
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndDate
Exclusive all-day end date in yyyy-MM-dd form.

```yaml
Type: String
Parameter Sets: CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EndTime
Timed-event end including an offset.

```yaml
Type: DateTimeOffset
Parameter Sets: CreateTimed, UpdateTimed
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -EntityId
Calendar entity identifier.

```yaml
Type: String
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Location
Optional event location.

```yaml
Type: String
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecurrenceId
Optional recurring occurrence identifier.

```yaml
Type: String
Parameter Sets: UpdateTimed, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecurrenceRange
Optional provider recurrence range, such as THISANDFUTURE.

```yaml
Type: String
Parameter Sets: UpdateTimed, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RecurrenceRule
Optional iCalendar recurrence rule, such as FREQ=WEEKLY.

```yaml
Type: String
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartDate
All-day start date in yyyy-MM-dd form.

```yaml
Type: String
Parameter Sets: CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -StartTime
Timed-event start including an offset.

```yaml
Type: DateTimeOffset
Parameter Sets: CreateTimed, UpdateTimed
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Summary
Event summary or title.

```yaml
Type: String
Parameter Sets: CreateTimed, UpdateTimed, CreateAllDay, UpdateAllDay
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Uid
Existing event UID; supplying it selects an update parameter set.

```yaml
Type: String
Parameter Sets: UpdateTimed, UpdateAllDay
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

- `None`

## RELATED LINKS

- None
