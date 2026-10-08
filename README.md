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

## Server Configuration
```cfg
set fiveprs_db_type              "sqlite"   # or "mysql"
set fiveprs_db_connection        ""         # MySQL connection string, or a custom SQLite path
set fiveprs_max_xp               500        # cap on XP awarded per call
set fiveprs_xp_multiplier        1.0
set fiveprs_restrict_departments false      # true requires the department ACE below

add_ace group.admin fiveprs.admin allow
add_ace group.police fiveprs.department.police allow
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

## Joining & Entry Screen
When a player spawns, an entry screen shows their rank and XP and lets them pick a department and agency, then go on duty or continue as a civilian. Only departments the player is permitted to join are offered. Reopen it with `/fiveprs` while off duty, or turn it off with `"showEntryScreen": false` in `config/settings.json`.

## Loading Screen
`fiveprs_loadscreen` is an optional companion resource with a FivePRS-branded loading screen: load progress, rotating tips and a welcome back card with the player's agency and rank. Add `ensure fiveprs_loadscreen` to `server.cfg` to use it, and set your server name and tips in its `config.js`. Leave it out if your server already has a loading screen.

## Mobile Data Terminal
On-duty units open the MDT with `F7` or `/mdt`. It shows your unit and status, every active call with its territory and assigned units, and all units on duty. From the MDT you can set your status, attach to a call as backup and set a waypoint to a call. The interface lives in `nui/` as plain HTML, CSS and JavaScript, so it can be restyled without rebuilding the resource.

## Jurisdictions
`config/jurisdictions.json` defines territories as map polygons and the agencies that patrol them. Players choose an agency within their department with `/setagency`, and dispatch only offers calls to units inside their agency's territory. An agency with no territories is unrestricted. The bundled split (Los Santos for LSPD, Blaine County for BCSO) is a coarse default; adjust the polygons to suit your server.

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
