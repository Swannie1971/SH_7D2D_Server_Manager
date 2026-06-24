# 7 Days to Die Dedicated Server Backup & Restore Reference (v2.x)

## Server Configuration Context

- Server installed at custom path (example):
  `C:\GameServers\7DaysToDie\Test Server Alpha\`
- Started with:
  `7DaysToDieServer.exe -configfile=serverconfig.xml`
- `GameWorld = RWG`
- `GameName = Test Server Alpha`
- No `UserDataFolder` specified

---

## Default Save Location

When `UserDataFolder` is not specified, the dedicated server stores saves under:

```text
%APPDATA%\7DaysToDie\
```

Save path:

```text
%APPDATA%\7DaysToDie\Saves\<WorldName>\<GameName>\
```

Example:

```text
C:\Users\ServerUser\AppData\Roaming\7DaysToDie\Saves\WestXuyofuCounty\Test Server Alpha\
```

Notes:

- `GameWorld="RWG"` only indicates Random World Generation.
- The actual save folder uses the generated world name (e.g. `WestXuyofuCounty`).
- Save folder name is the configured `GameName`.

---

## Files Required For Full Backup

### Primary Save Folder

Backup:

```text
%APPDATA%\7DaysToDie\Saves\<WorldName>\<GameName>\
```

Typical contents:

```text
main.ttp
Player\
Region\
map_*.dat
```

### Player Data

```text
Player\*.ttp
```

Contains:

- Character progression
- Inventory
- Skills
- Quests
- Position
- Vehicle ownership

Player data alone is NOT sufficient for a full restore.

### Region Data

```text
Region\r.*.*.7rg
```

Contains:

- Terrain modifications
- Bases
- Player-built structures
- Containers
- Destroyed POIs
- Mining activity

These files are REQUIRED to preserve the world state.

### Global World State

```text
main.ttp
```

Contains:

- Current day
- Time
- Blood moon progression
- World state

Required.

### Map Data

```text
map_*.dat
```

Contains explored-map information.

Recommended but not strictly required.

---

## RWG Generated Worlds

For RWG worlds also back up:

```text
%APPDATA%\7DaysToDie\GeneratedWorlds\<WorldName>\
```

Example:

```text
C:\Users\ServerUser\AppData\Roaming\7DaysToDie\GeneratedWorlds\WestXuyofuCounty\
```

Contains files such as:

```text
biomes.png
prefabs.xml
spawnpoints.xml
splat3.png
...
```

This folder defines the generated world.

Recommended for every RWG backup.

---

## Recommended Complete Backup Set

Backup:

```text
%APPDATA%\7DaysToDie\Saves\<WorldName>\<GameName>\
```

Backup:

```text
%APPDATA%\7DaysToDie\GeneratedWorlds\<WorldName>\
```

Backup:

```text
<ServerInstall>\serverconfig.xml
```

Optional:

```text
<ServerInstall>\Mods\
```

---

## Did v2.x Change Save Locations?

No significant change.

Default user data location remains:

```text
%APPDATA%\7DaysToDie\
```

---

## Finding The Actual Save Path

Do not rely solely on XML.

Parse startup logs for:

```text
UserDataFolder
SaveGameFolder
GamePref.UserDataFolder
```

Typical examples:

```text
UserDataFolder:
C:\Users\ServerUser\AppData\Roaming\7DaysToDie
```

```text
SaveGameFolder:
C:\Users\ServerUser\AppData\Roaming\7DaysToDie\Saves
```

Recommended implementation:

1. Launch server.
2. Tail console/log output.
3. Regex match:
   - UserDataFolder
   - SaveGameFolder
4. Store resolved paths.

---

## Restore Procedure

1. Stop the server completely.
2. Verify process has exited.
3. Restore:

```text
Saves\<WorldName>\<GameName>\
```

4. Restore:

```text
GeneratedWorlds\<WorldName>\
```

5. Restore:

```text
serverconfig.xml
```

6. Verify:

```xml
<property name="GameWorld" value="WestXuyofuCounty"/>
<property name="GameName" value="Test Server Alpha"/>
```

7. Start server.

If names do not match, the server will create a new save instead of loading the restored one.

---

## Recommended Backup Algorithm For A Server Manager

```text
1. Determine UserDataFolder from server logs.
2. Resolve:
   UserDataFolder\Saves\<WorldName>\<GameName>
3. Resolve:
   UserDataFolder\GeneratedWorlds\<WorldName>
4. Stop server.
5. Create backup archive.
6. Include serverconfig.xml.
7. Restart server.
```
