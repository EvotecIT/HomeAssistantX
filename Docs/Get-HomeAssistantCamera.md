---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantCamera
## SYNOPSIS
Reads camera state, capabilities, stream details, preferences, or temporary signed paths.

## SYNTAX
### Status (Default)
```powershell
Get-HomeAssistantCamera [[-EntityId] <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Capabilities
```powershell
Get-HomeAssistantCamera [-EntityId] <string> -Capabilities [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Stream
```powershell
Get-HomeAssistantCamera [-EntityId] <string> -Stream [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Preferences
```powershell
Get-HomeAssistantCamera [-EntityId] <string> -Preferences [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### SignedImage
```powershell
Get-HomeAssistantCamera [-EntityId] <string> -SignedImage [-Width <Int32>] [-Height <Int32>] [-ExpiresInSeconds <Int32>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### SignedMjpeg
```powershell
Get-HomeAssistantCamera [-EntityId] <string> -SignedMjpeg [-IntervalSeconds <Double>] [-ExpiresInSeconds <Int32>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads camera state, capabilities, stream details, preferences, or temporary signed paths.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantCamera
```


### EXAMPLE 2
```powershell
Get-HomeAssistantCamera camera.front -Stream
```


## PARAMETERS

### -Capabilities
Specifies the capabilities switch.

```yaml
Type: SwitchParameter
Parameter Sets: Capabilities
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
Parameter Sets: Status, Capabilities, Stream, Preferences, SignedImage, SignedMjpeg
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -EntityId
Specifies a value for entity id.

```yaml
Type: String
Parameter Sets: Status, Capabilities, Stream, Preferences, SignedImage, SignedMjpeg
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ExpiresInSeconds
Specifies a value for expires in seconds.

```yaml
Type: Int32
Parameter Sets: SignedImage, SignedMjpeg
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Height
Specifies a value for height.

```yaml
Type: Int32
Parameter Sets: SignedImage
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -IntervalSeconds
Specifies a value for interval seconds.

```yaml
Type: Double
Parameter Sets: SignedMjpeg
Aliases: None
Possible values:

Required: False
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

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SignedImage
Specifies the signed image switch.

```yaml
Type: SwitchParameter
Parameter Sets: SignedImage
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -SignedMjpeg
Specifies the signed mjpeg switch.

```yaml
Type: SwitchParameter
Parameter Sets: SignedMjpeg
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Stream
Specifies the stream switch.

```yaml
Type: SwitchParameter
Parameter Sets: Stream
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Width
Specifies a value for width.

```yaml
Type: Int32
Parameter Sets: SignedImage
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

- `HomeAssistantX.Cameras.HomeAssistantCameraStatus`
- `HomeAssistantX.Cameras.HomeAssistantCameraCapabilities`
- `HomeAssistantX.Cameras.HomeAssistantCameraStream`
- `HomeAssistantX.Cameras.HomeAssistantCameraPreferences`
- `System.String`

## RELATED LINKS

- None
