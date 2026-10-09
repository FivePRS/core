![FivePRS Logo](https://cmap.pics/ibNxG/uXr_2c.png/raw) 

**FivePRS Core** is the primary framework engine designed for FiveM, providing a high performance, modular alternative for scripted emergency response gameplay.

> [!CAUTION]
> FivePRS is not yet ready for production use and is only public at this stage for transparency and community contributions/feedback.

## Project Status: Under Development
The core engine is currently in the architectural phase. We are focusing on:
* **Event Routing:** Efficient handling of multi departmental calls.
* **Unified API:** A robust set of tools for developers to build callouts.
* **Performance:** Minimizing resmon usage while maximizing immersion.

## Key Features (Planned)
- **Multi Agency Support:** Native logic for PD, Fire, and Medical.
- **Modular Design:** Load only the systems your server needs.
- **Dynamic AI:** Smarter NPC interactions and scene management.
- **Extensible:** Built to be easily expanded via the `addons` repository.

## Installation
Run `publish.ps1` to build `dist/fiveprs.zip`, then extract it into your server's `resources/` folder and `ensure fiveprs` after the configuration below.

Optional official addons, such as the `fiveprs_loadscreen` loading screen, are published separately as `fiveprs_addons.zip` from the [FivePRS addons repository](https://github.com/FivePRS/addons).

## Server Configuration
```cfg
set fiveprs_db_type              "sqlite"   # or "mysql"
set fiveprs_db_connection        ""         # MySQL connection string, or a custom SQLite path
set fiveprs_max_xp               500        # cap on XP awarded per call
set fiveprs_xp_multiplier        1.0
set fiveprs_restrict_departments false      # true limits departments to the ACE below or the admin roster
set fiveprs_server_icon          true       # use the FivePRS icon unless load_server_icon sets your own

add_ace group.admin fiveprs.admin allow
add_ace group.police fiveprs.department.police allow

ensure fiveprs
```

| Permission | Grants |
|---|---|
| `fiveprs.admin` | Admin commands, the Admin tab and access to every department |
| `fiveprs.department.<police\|ems\|fire>` | Joining that department when `fiveprs_restrict_departments` is `true` |

When `fiveprs_restrict_departments` is `true`, a player can join a department if they are an admin, hold its ACE, or are on its roster in the Admin tab.

Admin commands work from the server console or in game with `fiveprs.admin`:

| Command | Description |
|---|---|
| `fprs_units` | List on-duty units and their status |
| `fprs_setrank <id> <rank>` | Set a player's rank |
| `fprs_addxp <id> <amount>` | Grant XP |
| `fprs_setdept <id> <department>` | Move a player to a department |
| `fprs_offduty <id>` | Force a player off duty |
| `fprs_endcall <call id>` | Close an active call |

## FivePRS Menu
Press `F5` or use `/fiveprs` to open the FivePRS menu. It opens on the most relevant tab, and only shows tabs the player can use.

### Duty
The Duty tab shows the player's rank and XP and lets them pick a department, agency and callsign before going on duty. Only departments the player is permitted to join are offered. Callsigns are up to 12 letters, numbers or hyphens, are saved to the player's profile, and must be unique among units on duty; leaving it blank uses the agency's default (for example `LSPD-12`). While on duty, the tab shows the player's unit and a Go off duty button.

### Dispatch
On-duty units get a Dispatch tab, which the menu opens on by default. It shows your unit and status, any incoming call with Accept and Decline buttons and a countdown, your active call with an End call button, every active call with its territory and assigned units, and all units on duty. From here you can set your status, attach to a call as backup and set a waypoint to a call. The accept, decline and end call keys still work alongside the buttons.

The menu lives in `nui/` as plain HTML, CSS and JavaScript, so it can be restyled without rebuilding the resource.

### Records
On-duty police get a Records tab. Search for a person by name or a vehicle by plate to open their record: identity, an active-warrant flag, licenses, registered vehicles and history. From a record, officers can:

- add a citation (with a fine up to `maxFine` in `config/settings.json`), arrest, warning or warrant;
- mark an active warrant as served or cleared;
- issue, suspend, revoke or reinstate any license type, including ones that are not self-service;
- mark a registered vehicle stolen or recovered.

Every change is stamped with the officer's name and callsign and written to the audit log. If the person's owner is online, they get a notification and their Civilian tab updates. A character with an active warrant cannot be deleted.

### Civilian
Every player has a Civilian tab for roleplay records:

- **Characters:** create up to `maxCharacters` characters (name, date of birth, gender) and pick the active one. Deleting a character removes its licenses and vehicles.
- **Licenses:** license types come from `config/licenses.json`. Types with `"selfService": true` can be applied for from the tab and are issued immediately; others must be issued by law enforcement. Licenses can be valid, suspended or revoked.
- **Record:** a read-only history of citations, arrests, warnings and warrants added by law enforcement.
- **Vehicles:** sit in the driver's seat and register the vehicle; the server reads the plate from the vehicle itself, so registrations always match a real car. Each character can register up to `maxVehiclesPerCharacter` vehicles, plates are unique server-wide, and owners can report a vehicle stolen or recovered.

`maxCharacters` and `maxVehiclesPerCharacter` are set in `config/settings.json`.

### 911
Every player has a 911 tab to call Police, EMS or Fire from their current position, optionally as an anonymous caller. On-duty units in that department are notified and see the call in their Dispatch tab with the caller and description; it has no primary unit, and any unit can attach to respond. The caller is told when units respond and when the call is cleared, and can cancel it while it is open. An attached unit clears the call from the Dispatch tab when finished.

Each player can have one open 911 call at a time, with `emergencyCallCooldownSeconds` between calls. A call that nobody attaches to closes after `emergencyCallTimeoutMinutes`.

### Admin
Players with `fiveprs.admin` get an Admin tab. It lists online players and can search offline players by name, with buttons to add or remove each player from the Police, EMS and Fire rosters. Changes apply immediately: the player is notified, their department options update, and anyone removed from the department they are on duty in is taken off duty. Every change is written to the audit log.

The roster only takes effect when `fiveprs_restrict_departments` is `true`; the tab shows a warning while it is off.

## Addon API
Other client resources can read the local player's FivePRS state with `exports.fiveprs:getState()`. It returns `name`, `onDuty`, `department` and `rank`. While on duty, it also returns `agency`, `callsign`, `status` and `territory`, plus `callId` and `call` while assigned to a call. The official `fiveprs_presence` addon uses it for Discord rich presence.

## Branding
The menu nameplate, the notification logo, the server icon and the department icons are loaded from the FivePRS CDN. The URLs live under `branding` in `config/settings.json` and can point to your own images. The department icons under `branding.departments` (`police`, `ems`, `fire` and `civilian`) are shown on the Duty, Dispatch, Civilian and 911 tabs and in the station menu; a missing image is simply left out.

The server icon is downloaded on start and cached in `data/server_icon.png`, which is used if the download fails. It is only applied when `fiveprs_server_icon` is `true` and no icon has been loaded with `load_server_icon`.

## Loading Screen
FivePRS hands each joining player's name, agency and rank to the loading screen. The official `fiveprs_loadscreen` addon in the [addons repository](https://github.com/FivePRS/addons) uses it for a welcome back card; any loading screen can read it from `window.nuiHandoverData.fiveprs`.

## Stations
`config/stations.json` lists the police stations, fire stations and hospitals. Each has a map blip and a duty point marker. Set `blips` to `always`, `onDuty` (only your own department's stations while on duty) or `off`.

| Field | Description |
|---|---|
| `id`, `name` | Unique ID and display name |
| `department` | `Police`, `EMS` or `Fire` |
| `agencies` | Agency IDs that use the station; empty means every agency in the department |
| `dutyPoint`, `heading` | Marker position `[x, y, z]`, also where players teleport to |
| `armory`, `locker`, `garage` | Optional `[x, y, z]` markers for each service; without them, the services are offered at the duty point |
| `parking` | Optional vehicle spots `[x, y, z, heading]`; the first free one is used, otherwise the nearest road to the garage |
| `blip` | Optional `{ "sprite": 60, "color": 3 }` to override the department's default blip |

Pressing `E` on the duty point opens the Duty tab, or the station menu while on duty with that station's department. The station menu sits on the right of the screen and is used with the arrow keys or mouse wheel, Enter and Backspace:

- **Armory:** rank kits from `police_loadouts.json` (Recruit, Officer at rank 3, Senior Officer at rank 5, Command at rank 8), each weapon on its own, body armour, ammo refills and returning weapons. A weapon can set a `label` to change its menu name.
- **Locker:** the patrol uniform, the command uniform from rank 8, and your own clothes.
- **Garage:** every model from `police_vehicles.json` your rank unlocks, parked outside, and returning your vehicle when it is near the garage. You have one vehicle at a time.

To get coordinates, stand where you want a point and run `/fprs_coords` for a duty point, or `/fprs_coords armory`, `locker` or `garage`. Sit in a parked vehicle for a parking spot. The values are copied to the clipboard in `stations.json` format and printed to the F8 console.

When going on duty, the Duty tab asks where to start:

- **Here:** your rank's loadout, uniform and vehicle are issued. The vehicle spawns on the nearest road, or outside the station if you are within 75 m of one of your stations.
- **Station:** you are teleported to the chosen station to collect your gear.
- **Drive to station:** a GPS route is set to the chosen station to collect your gear.

`dutyEquipment` in `config/settings.json` changes this: `byStart` (the default) works as above, `always` issues gear on every start (with Drive to station, the vehicle appears when you get close), and `station` never issues gear automatically.

## Jurisdictions
`config/jurisdictions.json` defines territories as map polygons and the agencies that patrol them. Players choose an agency within their department from the duty menu, and dispatch only offers calls to units inside their agency's territory. An agency with no territories is unrestricted. The bundled split (Los Santos for LSPD, Blaine County for BCSO) is a coarse default; adjust the polygons to suit your server.

## Agency Loadouts & Vehicles
`config/police_loadouts.json` and `config/police_vehicles.json` define the default weapons, uniforms and patrol vehicles per rank tier. Any agency can override individual tiers under `agencies`; anything not overridden falls back to the defaults. BCSO ships with the `sheriff` and `sheriff2` vehicles.

Vehicles spawn as stock models. A tier can also set `primaryColor`, `secondaryColor`, `livery`, `dirtLevel`, `plateText`, `forcedExtras` and `disabledExtras`; anything left out is not changed.

Uniforms use freemode clothing slots by default, which keeps the player's character. To dress an agency in a ped model instead:

```json
"agencies": {
    "bcso": {
        "uniform": { "pedModels": { "male": "s_m_y_sheriff_01", "female": "s_f_y_sheriff_01" } }
    }
}
```

The original model and clothing are restored when going off duty, but freemode facial customisation is not, so ped models suit servers that do not use freemode characters.

Duty changes, department changes, XP awards, rank-ups, admin actions and denied permission checks are recorded in the `fiveprs_audit` table.

## Troubleshooting
**`Could not load assembly Windows` / `Microsoft.Windows.SDK.NET` stack traces on start.** These appear once each time the resource starts with SQLite and are harmless. `Microsoft.Data.Sqlite` checks whether it is running as a Windows Store app by looking for those assemblies; FiveM logs each failed lookup with a full stack trace, then SQLite carries on normally. Startup succeeded if `[FivePRS] Database (SQLite) ready.` follows them. MySQL does not produce these messages.

**`Could not load native SQLite` or `DllNotFoundException: e_sqlite3`.** The resource's `server/` folder must contain `e_sqlite3.dll` (Windows) or `libe_sqlite3.so` (Linux). Re-extract `fiveprs.zip` rather than copying individual DLLs.
