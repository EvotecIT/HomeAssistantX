---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantDashboard
## SYNOPSIS
Reads Home Assistant frontend panels, Lovelace dashboards, configurations, resources, or mode information.

## SYNTAX
### Dashboards (Default)
```powershell
Get-HomeAssistantDashboard [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Panels
```powershell
Get-HomeAssistantDashboard -Panels [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Info
```powershell
Get-HomeAssistantDashboard -Info [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Configuration
```powershell
Get-HomeAssistantDashboard -Configuration [-UrlPath <string>] [-ForceReload] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Resources
```powershell
Get-HomeAssistantDashboard -Resources [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads Home Assistant frontend panels, Lovelace dashboards, configurations, resources, or mode information.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantDashboard
```


### EXAMPLE 2
```powershell
Get-HomeAssistantDashboard -Configuration -UrlPath 'house-main'
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
Parameter Sets: Dashboards, Panels, Info, Configuration, Resources
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -ForceReload
Specifies the force reload switch.

```yaml
Type: SwitchParameter
Parameter Sets: Configuration
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Info
Specifies the info switch.

```yaml
Type: SwitchParameter
Parameter Sets: Info
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Panels
Specifies the panels switch.

```yaml
Type: SwitchParameter
Parameter Sets: Panels
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Resources
Specifies the resources switch.

```yaml
Type: SwitchParameter
Parameter Sets: Resources
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

- `HomeAssistantX.Dashboards.HomeAssistantDashboard`
- `HomeAssistantX.Dashboards.HomeAssistantPanel`
- `HomeAssistantX.Dashboards.HomeAssistantLovelaceInfo`
- `HomeAssistantX.Dashboards.HomeAssistantDashboardResource`
- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
