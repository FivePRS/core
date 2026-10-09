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
Run `publish.ps1` to build two bundles in `dist/`:

| Bundle | Contents |
|---|---|
| `fiveprs.zip` | The `fiveprs` resource, required |
| `fiveprs_addons.zip` | A `[fiveprs_addons]` folder with every optional addon, such as `fiveprs_loadscreen` |

Extract both into your server's `resources/` folder, then `ensure fiveprs` and each addon you want after the configuration below. Addons live in `addons/<resource_name>/` in this repository; any folder there with an `fxmanifest.lua` is bundled automatically.

## Server Configuration
```cfg
set fiveprs_db_type              "sqlite"   # or "mysql"
set fiveprs_db_connection        ""         # MySQL connection string, or a custom SQLite path
set fiveprs_max_xp               500        # cap on XP awarded per call
set fiveprs_xp_multiplier        1.0
set fiveprs_restrict_departments false      # true requires the department ACE below
set fiveprs_server_icon        true       # use the FivePRS icon unless load_server_icon sets your own

add_ace group.admin fiveprs.admin allow
add_ace group.police fiveprs.department.police allow

ensure fiveprs
ensure fiveprs_loadscreen
```

| Permission | Grants |
|---|---|
| `fiveprs.admin` | Admin commands and access to every department |
| `fiveprs.department.<police\|ems\|fire>` | Joining that department when `fiveprs_restrict_departments` is `true` |

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

## Loading Screen
`fiveprs_loadscreen` is an optional addon in `fiveprs_addons` with a FivePRS-branded loading screen: load progress, rotating tips and a welcome back card with the player's agency and rank. Add `ensure fiveprs_loadscreen` to `server.cfg` to use it, and set your server name and tips in its `config.js`. Leave it out if your server already has a loading screen.

For background music, put `.mp3` or `.ogg` files in the addon's `music/` folder and list them under `music.tracks` in `config.js`, along with `volume` and `shuffle`. Direct links to audio files also work. No music ships with FivePRS, so only use tracks you have the rights to. Players can mute it from the button in the top-right corner, and the choice is remembered.

## Jurisdictions
`config/jurisdictions.json` defines territories as map polygons and the agencies that patrol them. Players choose an agency within their department from the duty menu, and dispatch only offers calls to units inside their agency's territory. An agency with no territories is unrestricted. The bundled split (Los Santos for LSPD, Blaine County for BCSO) is a coarse default; adjust the polygons to suit your server.

## Agency Loadouts & Vehicles
`config/police_loadouts.json` and `config/police_vehicles.json` define the default weapons, uniforms and patrol vehicles per rank tier. Any agency can override individual tiers under `agencies`; anything not overridden falls back to the defaults. BCSO ships with the `sheriff` and `sheriff2` vehicles.

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
