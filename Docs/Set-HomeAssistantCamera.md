---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantCamera
## SYNOPSIS
Updates administrator-only camera streaming preferences.

## SYNTAX
### __AllParameterSets
```powershell
Set-HomeAssistantCamera [-EntityId] <string> [-PreloadStream <Boolean>] [-Orientation <HomeAssistantCameraOrientation>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Updates administrator-only camera streaming preferences.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantCamera camera.front -PreloadStream $true -WhatIf
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

### -Orientation
Specifies a value for orientation.

```yaml
Type: HomeAssistantCameraOrientation
Parameter Sets: __AllParameterSets
Aliases: None
Possible values: NoTransform, Mirror, Rotate180, Flip, RotateLeftAndFlip, RotateLeft, RotateRightAndFlip, RotateRight

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Specifies the pass thru switch.

```yaml
Type: SwitchParameter
Parameter Sets: __AllParameterSets
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PreloadStream
Specifies a Boolean value for preload stream.

```yaml
Type: Boolean
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

- `HomeAssistantX.Cameras.HomeAssistantCameraPreferences`

## RELATED LINKS

- None
