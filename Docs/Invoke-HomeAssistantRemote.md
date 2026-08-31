---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Invoke-HomeAssistantRemote
## SYNOPSIS
Controls a Home Assistant remote, including sending, learning, and deleting commands.

## SYNTAX
### Entity (Default)
```powershell
Invoke-HomeAssistantRemote [-Entity] <string[]> -Action <HomeAssistantRemoteAction> [-Activity <string>] [-Command <string[]>] [-RemoteDevice <string>] [-RepeatCount <Int32>] [-DelaySeconds <Double>] [-HoldSeconds <Double>] [-CommandType <HomeAssistantRemoteCommandType>] [-Alternative <Boolean>] [-TimeoutSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Invoke-HomeAssistantRemote -Action <HomeAssistantRemoteAction> -InputObject <HomeAssistantEntityInfo[]> [-Activity <string>] [-Command <string[]>] [-RemoteDevice <string>] [-RepeatCount <Int32>] [-DelaySeconds <Double>] [-HoldSeconds <Double>] [-CommandType <HomeAssistantRemoteCommandType>] [-Alternative <Boolean>] [-TimeoutSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Invoke-HomeAssistantRemote [-Area] <string[]> -Action <HomeAssistantRemoteAction> [-Activity <string>] [-Command <string[]>] [-RemoteDevice <string>] [-RepeatCount <Int32>] [-DelaySeconds <Double>] [-HoldSeconds <Double>] [-CommandType <HomeAssistantRemoteCommandType>] [-Alternative <Boolean>] [-TimeoutSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Invoke-HomeAssistantRemote [-Device] <string[]> -Action <HomeAssistantRemoteAction> [-Activity <string>] [-Command <string[]>] [-RemoteDevice <string>] [-RepeatCount <Int32>] [-DelaySeconds <Double>] [-HoldSeconds <Double>] [-CommandType <HomeAssistantRemoteCommandType>] [-Alternative <Boolean>] [-TimeoutSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Invoke-HomeAssistantRemote [-Floor] <string[]> -Action <HomeAssistantRemoteAction> [-Activity <string>] [-Command <string[]>] [-RemoteDevice <string>] [-RepeatCount <Int32>] [-DelaySeconds <Double>] [-HoldSeconds <Double>] [-CommandType <HomeAssistantRemoteCommandType>] [-Alternative <Boolean>] [-TimeoutSeconds <Double>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Controls a Home Assistant remote, including sending, learning, and deleting commands.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HomeAssistantRemote -Entity remote.living_room -Action SendCommand -Command Power -RepeatCount 2 -WhatIf
```


### EXAMPLE 2
```powershell
Invoke-HomeAssistantRemote -Entity remote.harmony -Action TurnOn -Activity 'Watch TV'
```


## PARAMETERS

### -Action
Remote operation to perform.

```yaml
Type: HomeAssistantRemoteAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: TurnOn, TurnOff, Toggle, SendCommand, LearnCommand, DeleteCommand

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Activity
Activity passed to a remote power operation.

```yaml
Type: String
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Alternative
Requests the integration's alternative learning mode.

```yaml
Type: Boolean
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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

### -Command
One or more commands to send, learn, or delete.

```yaml
Type: String[]
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -CommandType
IR or RF command type used while learning.

```yaml
Type: HomeAssistantRemoteCommandType
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Ir, Rf

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
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -DelaySeconds
Delay between repeated sent commands, in seconds.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
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

### -HoldSeconds
Duration for which a sent command is held, in seconds.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
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

### -RemoteDevice
Optional receiver or device known by the remote integration.

```yaml
Type: String
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: DeviceName
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RepeatCount
Number of times each sent command is repeated.

```yaml
Type: Int32
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -TimeoutSeconds
Learning timeout in seconds.

```yaml
Type: Double
Parameter Sets: Entity, InputObject, Area, Device, Floor
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
