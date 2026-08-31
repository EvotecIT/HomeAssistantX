---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Send-HomeAssistantNotification
## SYNOPSIS
Sends a persistent notification or a message to selected notify entities.

## SYNTAX
### Entity (Default)
```powershell
Send-HomeAssistantNotification [-Entity] <string[]> -Message <string> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Persistent
```powershell
Send-HomeAssistantNotification -Persistent -Message <string> [-Title <string>] [-NotificationId <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Send-HomeAssistantNotification -Message <string> -InputObject <HomeAssistantEntityInfo[]> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Send-HomeAssistantNotification [-Area] <string[]> -Message <string> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Send-HomeAssistantNotification [-Device] <string[]> -Message <string> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Send-HomeAssistantNotification [-Floor] <string[]> -Message <string> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Send-HomeAssistantNotification [-Label] <string[]> -Message <string> [-Title <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Sends a persistent notification or a message to selected notify entities.

## EXAMPLES

### EXAMPLE 1
```powershell
Send-HomeAssistantNotification -Persistent -Message 'Garage is open' -Title Security
```


### EXAMPLE 2
```powershell
Send-HomeAssistantNotification -Area Kitchen -Message 'Dinner is ready' -WhatIf
```


## PARAMETERS

### -Area
One or more area names, aliases, or native area IDs. Room is an alias.

```yaml
Type: String[]
Parameter Sets: Area
Aliases: AreaId, Room
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Entity, Persistent, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Device
One or more device friendly names or native device IDs.

```yaml
Type: String[]
Parameter Sets: Device
Aliases: DeviceId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Entity
One or more entity friendly names or native entity IDs.

```yaml
Type: String[]
Parameter Sets: Entity
Aliases: EntityId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Floor
One or more floor names, aliases, or native floor IDs.

```yaml
Type: String[]
Parameter Sets: Floor
Aliases: FloorId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputObject
Joined entities accepted from Get-HomeAssistantEntity.

```yaml
Type: HomeAssistantEntityInfo[]
Parameter Sets: InputObject
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Label
One or more label names or native label IDs.

```yaml
Type: String[]
Parameter Sets: Label
Aliases: LabelId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Message
Notification message.

```yaml
Type: String
Parameter Sets: Entity, Persistent, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NotificationId
Stable persistent notification ID. Reusing it updates the existing notification.

```yaml
Type: String
Parameter Sets: Persistent
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Persistent
Creates a notification stored in the Home Assistant interface.

```yaml
Type: SwitchParameter
Parameter Sets: Persistent
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Optional notification title.

```yaml
Type: String
Parameter Sets: Entity, Persistent, InputObject, Area, Device, Floor, Label
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

- `HomeAssistantX.Inventory.HomeAssistantEntityInfo[]`
- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
