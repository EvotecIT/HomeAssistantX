---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Set-HomeAssistantDashboard
## SYNOPSIS
Creates or updates Lovelace dashboards, configurations, and storage-mode resources.

## SYNTAX
### Configuration (Default)
```powershell
Set-HomeAssistantDashboard -ConfigurationJson <string> [-UrlPath <string>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Create
```powershell
Set-HomeAssistantDashboard -UrlPath <string> -New -Title <string> [-Icon <string>] [-HideFromSidebar] [-RequireAdmin] [-AllowSingleWord] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Update
```powershell
Set-HomeAssistantDashboard -DashboardId <string> [-Title <string>] [-Icon <string>] [-RemoveIcon] [-ShowInSidebar <Boolean>] [-DashboardRequireAdmin <Boolean>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ResourceCreate
```powershell
Set-HomeAssistantDashboard -NewResource -ResourceUrl <string> -ResourceType <HomeAssistantDashboardResourceType> [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ResourceUpdate
```powershell
Set-HomeAssistantDashboard -ResourceId <string> [-ResourceUrl <string>] [-ResourceType <HomeAssistantDashboardResourceType>] [-PassThru] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Creates or updates Lovelace dashboards, configurations, and storage-mode resources.

## EXAMPLES

### EXAMPLE 1
```powershell
Set-HomeAssistantDashboard -ConfigurationJson '{"views":[]}' -WhatIf
```


### EXAMPLE 2
```powershell
Set-HomeAssistantDashboard -New -UrlPath 'house-main' -Title 'House' -WhatIf
```


### EXAMPLE 3
```powershell
Set-HomeAssistantDashboard -NewResource -ResourceUrl '/local/card.js' -ResourceType Module -WhatIf
```


## PARAMETERS

### -AllowSingleWord
Specifies the allow single word switch.

```yaml
Type: SwitchParameter
Parameter Sets: Create
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ConfigurationJson
Specifies a value for configuration json.

```yaml
Type: String
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
Parameter Sets: Configuration, Create, Update, ResourceCreate, ResourceUpdate
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
Parameter Sets: Update
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -DashboardRequireAdmin
Specifies a Boolean value for dashboard require admin.

```yaml
Type: Boolean
Parameter Sets: Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -HideFromSidebar
Specifies the hide from sidebar switch.

```yaml
Type: SwitchParameter
Parameter Sets: Create
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Icon
Specifies a value for icon.

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

### -New
Specifies the new switch.

```yaml
Type: SwitchParameter
Parameter Sets: Create
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -NewResource
Specifies the new resource switch.

```yaml
Type: SwitchParameter
Parameter Sets: ResourceCreate
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -PassThru
Specifies the pass thru switch.

```yaml
Type: SwitchParameter
Parameter Sets: Configuration, Create, Update, ResourceCreate, ResourceUpdate
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RemoveIcon
Specifies the remove icon switch.

```yaml
Type: SwitchParameter
Parameter Sets: Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -RequireAdmin
Specifies the require admin switch.

```yaml
Type: SwitchParameter
Parameter Sets: Create
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceId
Specifies a value for resource id.

```yaml
Type: String
Parameter Sets: ResourceUpdate
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceType
Specifies a value for resource type.

```yaml
Type: HomeAssistantDashboardResourceType
Parameter Sets: ResourceCreate, ResourceUpdate
Aliases: None
Possible values: JavaScript, Css, Module, Html

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ResourceUrl
Specifies a value for resource url.

```yaml
Type: String
Parameter Sets: ResourceCreate, ResourceUpdate
Aliases: None
Possible values:

Required: True
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ShowInSidebar
Specifies a Boolean value for show in sidebar.

```yaml
Type: Boolean
Parameter Sets: Update
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Title
Specifies a value for title.

```yaml
Type: String
Parameter Sets: Create, Update
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
Parameter Sets: Configuration, Create
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

- `HomeAssistantX.Dashboards.HomeAssistantDashboard`
- `HomeAssistantX.Dashboards.HomeAssistantDashboardResource`
- `System.Text.Json.JsonElement`

## RELATED LINKS

- None
