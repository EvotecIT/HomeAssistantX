---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantCategory
## SYNOPSIS
Creates or updates a Home Assistant category within an explicit scope.

## SYNTAX
### Create (Default)
```powershell
Set-HomeAssistantCategory [-Scope] <string> [-Name] <string> [-Icon <string>] [-ClearIcon] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Update
```powershell
Set-HomeAssistantCategory [-Scope] <string> -CategoryId <string> [-Name <string>] [-Icon <string>] [-ClearIcon] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates or updates a Home Assistant category within an explicit scope.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantCategory -Scope automation -Name Comfort -Icon mdi:sofa
```


### EXAMPLE 2
```powershell
Set-HomeAssistantCategory -Scope automation -CategoryId comfort -ClearIcon -WhatIf
```


## PARAMETERS

### -CategoryId
Native category ID; supplying it selects the update parameter set.

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

### -Name
Category name. Mandatory when creating and optional when updating.

```yaml
Type: String
Parameter Sets: Create, Update
Aliases: None
Possible values:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Scope
Category registry scope, such as automation or script.

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

- `HomeAssistantX.Registries.HomeAssistantCategory`

## RELATED LINKS

- None
