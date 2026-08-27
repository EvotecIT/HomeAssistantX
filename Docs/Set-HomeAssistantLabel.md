---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantLabel
## SYNOPSIS
Creates or updates a Home Assistant label while allowing nullable fields to be explicitly cleared.

## SYNTAX
### Create (Default)
```powershell
Set-HomeAssistantLabel [-Name] <string> [-Color <string>] [-Description <string>] [-Icon <string>] [-ClearColor] [-ClearDescription] [-ClearIcon] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Update
```powershell
Set-HomeAssistantLabel -LabelId <string> [-Name <string>] [-Color <string>] [-Description <string>] [-Icon <string>] [-ClearColor] [-ClearDescription] [-ClearIcon] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates or updates a Home Assistant label while allowing nullable fields to be explicitly cleared.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantLabel -Name Security -Color red -Icon mdi:shield
```


### EXAMPLE 2
```powershell
Set-HomeAssistantLabel -LabelId security -ClearColor -Description 'Safety devices' -WhatIf
```


## PARAMETERS

### -ClearColor
Clears the current color. Mutually exclusive with Color.

```yaml
Type: SwitchParameter
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearDescription
Clears the current description. Mutually exclusive with Description.

```yaml
Type: SwitchParameter
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ClearIcon
Clears the current icon. Mutually exclusive with Icon.

```yaml
Type: SwitchParameter
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Color
Theme color name or #RRGGBB color.

```yaml
Type: String
Parameter Sets: Create, Update
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
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -Description
Optional label description.

```yaml
Type: String
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Icon
Material Design icon identifier.

```yaml
Type: String
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -LabelId
Native label ID; supplying it selects the update parameter set.

```yaml
Type: String
Parameter Sets: Update
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Name
Label name. Mandatory when creating and optional when updating.

```yaml
Type: String
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Registries.HomeAssistantLabel`

## RELATED LINKS

- None
