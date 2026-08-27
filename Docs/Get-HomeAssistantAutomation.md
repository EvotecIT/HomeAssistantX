---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Get-HomeAssistantAutomation
## SYNOPSIS
Reads automation runtime state or an administrator-only editable configuration.

## SYNTAX
### Status (Default)
```powershell
Get-HomeAssistantAutomation [[-EntityId] <string>] [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

### Configuration
```powershell
Get-HomeAssistantAutomation [-AutomationId] <string> -Configuration [-Connection <HomeAssistantConnection>] [<CommonParameters>]
```

## DESCRIPTION
Reads automation runtime state or an administrator-only editable configuration.

## EXAMPLES

### EXAMPLE 1
```powershell
Get-HomeAssistantAutomation
```


### EXAMPLE 2
```powershell
Get-HomeAssistantAutomation -AutomationId 'morning-routine' -Configuration
```


## PARAMETERS

### -AutomationId
Specifies a value for automation id.

```yaml
Type: String
Parameter Sets: Configuration
Aliases: None
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

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
Parameter Sets: Status, Configuration
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
Parameter Sets: Status
Aliases: None
Possible values:

Required: False
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

- `HomeAssistantX.Automations.HomeAssistantAutomationStatus`
- `HomeAssistantX.Automations.HomeAssistantAutomationConfiguration`

## RELATED LINKS

- None
