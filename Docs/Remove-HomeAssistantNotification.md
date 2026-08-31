---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Remove-HomeAssistantNotification
## SYNOPSIS
Dismisses one or all persistent Home Assistant notifications.

## SYNTAX
### Id (Default)
```powershell
Remove-HomeAssistantNotification [-NotificationId] <string> [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### All
```powershell
Remove-HomeAssistantNotification -All [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Dismisses one or all persistent Home Assistant notifications.

## EXAMPLES

### EXAMPLE 1
```powershell
Remove-HomeAssistantNotification -NotificationId garage-open -WhatIf
```


### EXAMPLE 2
```powershell
Remove-HomeAssistantNotification -All -WhatIf
```


## PARAMETERS

### -All
Dismisses every current persistent notification.

```yaml
Type: SwitchParameter
Parameter Sets: All
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
Parameter Sets: Id, All
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: True (ByValue)
Accept wildcard characters: False
```

### -NotificationId
Persistent notification ID to dismiss.

```yaml
Type: String
Parameter Sets: Id
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

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
