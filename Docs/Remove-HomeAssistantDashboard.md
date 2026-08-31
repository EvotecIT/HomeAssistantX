---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Remove-HomeAssistantDashboard
## SYNOPSIS
Removes a Lovelace dashboard, its configuration, or a storage-mode resource.

## SYNTAX
### Dashboard (Default)
```powershell
Remove-HomeAssistantDashboard [-DashboardId] <string> [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Configuration
```powershell
Remove-HomeAssistantDashboard -Configuration [-UrlPath <string>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Resource
```powershell
Remove-HomeAssistantDashboard -ResourceId <string> [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Removes a Lovelace dashboard, its configuration, or a storage-mode resource.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-HomeAssistantDashboard -Configuration -UrlPath house-main -WhatIf
```


## PARAMETERS

### -Configuration
Specifies the configuration switch.

```yaml
Type: SwitchParameter
Parameter Sets: Configuration
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
Parameter Sets: Dashboard, Configuration, Resource
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -DashboardId
Specifies a value for dashboard id.

```yaml
Type: String
Parameter Sets: Dashboard
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceId
Specifies a value for resource id.

```yaml
Type: String
Parameter Sets: Resource
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -UrlPath
Specifies a value for url path.

```yaml
Type: String
Parameter Sets: Configuration
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

- `None`

## RELATED LINKS

- None
