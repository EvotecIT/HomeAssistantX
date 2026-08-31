---
external help file: HomeAssistantX-help.xml
Module Name: HomeAssistantX
online version: https://github.com/EvotecIT/HomeAssistantX
schema: 2.0.0
---
# Invoke-HomeAssistantRoutine
## SYNOPSIS
Runs a scene, script, or button routine through one task-oriented command.

## SYNTAX
### Entity (Default)
```powershell
Invoke-HomeAssistantRoutine [-Entity] <string[]> -Action <HomeAssistantRoutineAction> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### InputObject
```powershell
Invoke-HomeAssistantRoutine -Action <HomeAssistantRoutineAction> -InputObject <HomeAssistantEntityInfo[]> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Area
```powershell
Invoke-HomeAssistantRoutine [-Area] <string[]> -Action <HomeAssistantRoutineAction> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Device
```powershell
Invoke-HomeAssistantRoutine [-Device] <string[]> -Action <HomeAssistantRoutineAction> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Floor
```powershell
Invoke-HomeAssistantRoutine [-Floor] <string[]> -Action <HomeAssistantRoutineAction> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Label
```powershell
Invoke-HomeAssistantRoutine [-Label] <string[]> -Action <HomeAssistantRoutineAction> [-Transition <TimeSpan>] [-Variables <hashtable>] [-Connection <HomeAssistantConnection>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Runs a scene, script, or button routine through one task-oriented command.

## EXAMPLES

### EXAMPLE 1
```powershell
Invoke-HomeAssistantRoutine -Entity scene.evening -Action ActivateScene -WhatIf
```


### EXAMPLE 2
```powershell
Invoke-HomeAssistantRoutine -Entity script.welcome -Action RunScript -Variables @{ name = 'Alex' }
```


## PARAMETERS

### -Action
Scene, script, or button operation to run.

```yaml
Type: HomeAssistantRoutineAction
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values: ActivateScene, RunScript, StopScript, ToggleScript, PressButton, PressInputButton

Required: True
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

### -Connection
Optional explicit session returned by Connect-HomeAssistant. It also accepts pipeline input.

```yaml
Type: HomeAssistantConnection
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
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

### -Label
One or more label names or native label IDs.

```yaml
Type: String[]
Parameter Sets: Label
Aliases: LabelId
Possible values:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Transition
Optional scene transition duration.

```yaml
Type: TimeSpan
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
Aliases: None
Possible values:

Required: False
Position: named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Variables
Variables supplied when running a script.

```yaml
Type: Hashtable
Parameter Sets: Entity, InputObject, Area, Device, Floor, Label
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

- `HomeAssistantX.Inventory.HomeAssistantEntityInfo[]`
- `HomeAssistantX.PowerShell.HomeAssistantConnection`: An explicit, disposable Home Assistant session passed between cmdlets.

## OUTPUTS

- `HomeAssistantX.Services.HomeAssistantServiceCallResult`

## RELATED LINKS

- None
