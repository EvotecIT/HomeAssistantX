---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantMediaPlayer
## SYNOPSIS
Controls media-player power, playback, volume, source, grouping, queueing, and content.

## SYNTAX
### Entity (Default)
```powershell
Set-HomeAssistantMediaPlayer [-Entity] <string[]> [-Power <HomeAssistantPowerAction>] [-Playback <HomeAssistantMediaPlaybackAction>] [-VolumePercent <Double>] [-VolumeStep <HomeAssistantMediaVolumeStepAction>] [-Muted <Boolean>] [-Source <string>] [-SoundMode <string>] [-Shuffle <Boolean>] [-Repeat <HomeAssistantMediaRepeatMode>] [-SeekSeconds <Double>] [-ClearPlaylist] [-JoinMember <string[]>] [-Unjoin] [-MediaContentId <string>] [-MediaContentType <string>] [-Enqueue <HomeAssistantMediaEnqueueMode>] [-Announce] [-MediaExtra <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Set-HomeAssistantMediaPlayer -InputObject <HomeAssistantEntityInfo[]> [-Power <HomeAssistantPowerAction>] [-Playback <HomeAssistantMediaPlaybackAction>] [-VolumePercent <Double>] [-VolumeStep <HomeAssistantMediaVolumeStepAction>] [-Muted <Boolean>] [-Source <string>] [-SoundMode <string>] [-Shuffle <Boolean>] [-Repeat <HomeAssistantMediaRepeatMode>] [-SeekSeconds <Double>] [-ClearPlaylist] [-JoinMember <string[]>] [-Unjoin] [-MediaContentId <string>] [-MediaContentType <string>] [-Enqueue <HomeAssistantMediaEnqueueMode>] [-Announce] [-MediaExtra <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Set-HomeAssistantMediaPlayer [-Area] <string[]> [-Power <HomeAssistantPowerAction>] [-Playback <HomeAssistantMediaPlaybackAction>] [-VolumePercent <Double>] [-VolumeStep <HomeAssistantMediaVolumeStepAction>] [-Muted <Boolean>] [-Source <string>] [-SoundMode <string>] [-Shuffle <Boolean>] [-Repeat <HomeAssistantMediaRepeatMode>] [-SeekSeconds <Double>] [-ClearPlaylist] [-JoinMember <string[]>] [-Unjoin] [-MediaContentId <string>] [-MediaContentType <string>] [-Enqueue <HomeAssistantMediaEnqueueMode>] [-Announce] [-MediaExtra <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Set-HomeAssistantMediaPlayer [-Device] <string[]> [-Power <HomeAssistantPowerAction>] [-Playback <HomeAssistantMediaPlaybackAction>] [-VolumePercent <Double>] [-VolumeStep <HomeAssistantMediaVolumeStepAction>] [-Muted <Boolean>] [-Source <string>] [-SoundMode <string>] [-Shuffle <Boolean>] [-Repeat <HomeAssistantMediaRepeatMode>] [-SeekSeconds <Double>] [-ClearPlaylist] [-JoinMember <string[]>] [-Unjoin] [-MediaContentId <string>] [-MediaContentType <string>] [-Enqueue <HomeAssistantMediaEnqueueMode>] [-Announce] [-MediaExtra <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Set-HomeAssistantMediaPlayer [-Floor] <string[]> [-Power <HomeAssistantPowerAction>] [-Playback <HomeAssistantMediaPlaybackAction>] [-VolumePercent <Double>] [-VolumeStep <HomeAssistantMediaVolumeStepAction>] [-Muted <Boolean>] [-Source <string>] [-SoundMode <string>] [-Shuffle <Boolean>] [-Repeat <HomeAssistantMediaRepeatMode>] [-SeekSeconds <Double>] [-ClearPlaylist] [-JoinMember <string[]>] [-Unjoin] [-MediaContentId <string>] [-MediaContentType <string>] [-Enqueue <HomeAssistantMediaEnqueueMode>] [-Announce] [-MediaExtra <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Controls media-player power, playback, volume, source, grouping, queueing, and content.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantMediaPlayer -Area LivingRoom -VolumePercent 30 -Playback Play -WhatIf
```


### EXAMPLE 2
```powershell
Set-HomeAssistantMediaPlayer -Entity media_player.kitchen -MediaContentId 'media-source://media_source/local/dinner.mp3' -MediaContentType music -Announce
```


## PARAMETERS

### -Announce
Requests announcement playback. Home Assistant does not allow this with Enqueue.

```yaml
Type: SwitchParameter
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

### -ClearPlaylist
Clears the target playlist.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, InputObject, Area, Device, Floor
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
Parameter Sets: Entity, InputObject, Area, Device, Floor
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

### -Enqueue
Controls where content is placed in the target's queue.

```yaml
Type: HomeAssistantMediaEnqueueMode
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Add, Next, Play, Replace

Required: False
Position: named
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

### -JoinMember
Media-player entity identifiers to join to the selected group leader.

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

### -MediaContentId
Content identifier passed to media_player.play_media.

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

### -MediaContentType
Content type paired with MediaContentId.

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

### -MediaExtra
Provider-specific extra play-media values. Use only when the integration requires them.

```yaml
Type: Hashtable
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Muted
Sets or clears mute.

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

### -Playback
Optional playback action.

```yaml
Type: HomeAssistantMediaPlaybackAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Play, Pause, PlayPause, Stop, Next, Previous

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Power
Optional power action.

```yaml
Type: HomeAssistantPowerAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: On, Off, Toggle

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Repeat
Sets the repeat mode.

```yaml
Type: HomeAssistantMediaRepeatMode
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Off, One, All

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SeekSeconds
Seeks to an absolute position in seconds.

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

### -Shuffle
Enables or disables shuffle.

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

### -SoundMode
Sound mode exposed by the target.

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

### -Source
Input source exposed by the target.

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

### -Unjoin
Removes the selected media players from their groups.

```yaml
Type: SwitchParameter
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -VolumePercent
Volume from 0 through 100 percent.

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

### -VolumeStep
Raises or lowers the volume by the target's native step.

```yaml
Type: HomeAssistantMediaVolumeStepAction
Parameter Sets: Entity, InputObject, Area, Device, Floor
Aliases: None
Possible values: Up, Down

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
