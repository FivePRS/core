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
add_ace resource.fiveprs command.load_server_icon allow   # lets FivePRS set the server icon

ensure fiveprs
```

| Permission | Grants |
|---|---|
| `fiveprs.admin` | Admin commands, the Admin app and access to every department |
| `fiveprs.department.<police\|ems\|fire>` | Joining that department when `fiveprs_restrict_departments` is `true` |

When `fiveprs_restrict_departments` is `true`, a player can join a department if they are an admin, hold its ACE, or are on its roster in the Admin app.

Admin commands work from the server console or in game with `fiveprs.admin`:

| Command | Description |
|---|---|
| `fprs_units` | List on-duty units and their status |
| `fprs_setrank <id> <rank>` | Set a player's rank |
| `fprs_addxp <id> <amount>` | Grant XP |
| `fprs_setdept <id> <department>` | Move a player to a department |
| `fprs_offduty <id>` | Force a player off duty |
| `fprs_endcall <call id>` | Close an active call |

## FivePRS Terminal
Press `F5` or use `/fiveprs` to open the FivePRS terminal, a rugged in-car tablet. It opens on a home screen with a status bar (your callsign and status, or your active character) and an app for each feature you can use: Duty, Dispatch, Records, Characters, Licenses, Vehicles, 911, Admin and Settings. Apps only appear when you can use them, Dispatch shows a badge for an incoming or active call, and 911 shows one while your call is open. **Home** or `Escape` returns to the home screen; `Escape` on the home screen closes the terminal.

### Settings
The Settings app holds per-player options. While on duty it has the **AI callouts** switch. Players choose a wallpaper from the defaults or, if allowed, paste any https image link; the choice is saved to their profile on the server.

Wallpapers are configured under `terminal` in `config/settings.json`: `wallpapers` lists the built-in choices (`id`, `label`, `url`), `defaultWallpaper` is the id used until a player picks one, and `allowCustomWallpapers` set to `false` limits players to that list.

### Making terminal apps
Anyone can add apps to the terminal, either as a C# DLL dropped into `apps/` or from any resource through exports. Both kinds get a home screen icon, a badge and the Home button, and can either describe a screen that FivePRS draws in the terminal's style, or show their own HTML page.

**C# app (`apps/*.net.dll`).** Reference `client/FivePRS.Core.dll` and `client/FivePRS.Client.net.dll`, build a net452 class library named `<Name>.net.dll`, and subclass `TerminalApp`:

```csharp
using System.Collections.Generic;
using FivePRS.Client.Terminal;
using FivePRS.Core.Models;

public class TowApp : TerminalApp
{
    public override string Id    => "tow";
    public override string Label => "Tow";
    public override string Color => "#ca8a04";
    public override string Icon  => "M3 17h13l3-5h2v5 M7.5 19a2 2 0 1 0 0-4 2 2 0 0 0 0 4z";

    public override bool IsAvailable(PlayerData player) => player.IsOnDuty;

    public override AppScreen BuildScreen() => new AppScreen()
        .Add(AppBlock.Section("Request a tow"))
        .Add(AppBlock.Buttons(new AppItem { Title = "Call tow truck", Action = "call", Style = "primary" }));

    public override void OnAction(string action, IDictionary<string, object> data)
    {
        if (action == "call") Refresh();
    }
}
```

Screens are built from blocks: `Section`, `Paragraph`, `Buttons`, `List` (rows or a grid, with optional images) and `Form`. Every button, list item and form sends its `Action` and `Data` to `OnAction`. `Icon` is SVG path data on a 24×24 grid or an image URL; `Order` places the app on the home screen (built-in apps use 0 to 80, Settings is 1000); `Badge` shows a badge. For a fully custom screen, set `Page` to an HTML file such as `apps/tow/index.html` and put the page in `apps/tow/`.

**Resource app (any language).** Register from a client script:

```lua
exports.fiveprs:registerApp({
    id = 'notes', label = 'Notes', color = '#0891b2', order = 600,
    icon = 'M5 3h10l4 4v14H5z M9 9h6 M9 13h6',
    page = ('https://cfx-nui-%s/html/index.html'):format(GetCurrentResourceName()),
})

AddEventHandler('fiveprs:appAction', function(app, action, data)
    if app ~= 'notes' then return end
    exports.fiveprs:sendAppMessage('notes', 'saved', { ok = true })
end)
```

Leave out `page` and call `exports.fiveprs:setAppView(id, screen)` to have FivePRS draw a screen instead, using the same blocks as tables (`{ blocks = { { type = 'section', title = 'Notes' }, ... } }`). `setAppBadge(id, text)`, `setAppAvailable(id, bool)` and `unregisterApp(id)` update the app, and apps are removed when their resource stops.

**App pages.** An app page includes `https://cfx-nui-fiveprs/nui/app-sdk.js`, which provides `FivePRS.on(type, handler)` for messages, `FivePRS.action(name, data)` to send an action to the app, `FivePRS.home()` to return to the home screen, and `FivePRS.context`, the player's FivePRS state (the same as `exports.fiveprs:getState()`), which is also delivered as a `context` message whenever it changes. Messages from a C# app's `Send(type, payload)` or a resource's `sendAppMessage` arrive through `FivePRS.on`. `Escape` inside an app page returns to the home screen.

The `fiveprs_notes` addon in the [addons repository](https://github.com/FivePRS/addons) is a complete resource app to copy from.

### Duty
The Duty app shows the player's rank and XP and lets them pick a department, agency and callsign before going on duty. Only departments the player is permitted to join are offered. Callsigns are up to 12 letters, numbers or hyphens, are saved to the player's profile, and must be unique among units on duty; leaving it blank uses the agency's default (for example `LSPD-12`). While on duty, the tab shows the player's unit and a Go off duty button.

### Dispatch
On-duty units get a Dispatch app. It shows your unit and status, any incoming call with Accept and Decline buttons and a countdown, your active call with an End call button, every active call with its territory and assigned units, and all units on duty. From here you can set your status, attach to a call as backup and set a waypoint to a call. The AI callouts switch in the Settings app stops dispatch offering you AI callouts while player 911 calls still reach you, for servers that mix player and NPC roleplay; the choice is remembered, and `/er_aicallouts` toggles it (bindable under FivePRS in the key bindings). The accept, decline and end call keys still work alongside the buttons.

The menu lives in `nui/` as plain HTML, CSS and JavaScript, so it can be restyled without rebuilding the resource.

### Records
On-duty police get a Records app. Search for a person by name or a vehicle by plate to open their record: identity, an active-warrant flag, licenses, registered vehicles and history. From a record, officers can:

- add a citation (with a fine up to `maxFine` in `config/settings.json`), arrest, warning or warrant;
- mark an active warrant as served or cleared;
- issue, suspend, revoke or reinstate any license type, including ones that are not self-service;
- mark a registered vehicle stolen or recovered.

Every change is stamped with the officer's name and callsign and written to the audit log. If the person's owner is online, they get a notification and their Characters app updates. A character with an active warrant cannot be deleted.

### Characters, Licenses and Vehicles
Every player has three civilian apps for roleplay records, all for their active character:

- **Characters:** create up to `maxCharacters` characters (name, date of birth, gender) and pick the active one. Deleting a character removes its licenses and vehicles.
- **Licenses:** license types come from `config/licenses.json`. Types with `"selfService": true` can be applied for from the tab and are issued immediately; others must be issued by law enforcement. Licenses can be valid, suspended or revoked.
- **Record:** a read-only history of citations, arrests, warnings and warrants added by law enforcement.
- **Vehicles:** sit in the driver's seat and register the vehicle; the server reads the plate from the vehicle itself, so registrations always match a real car. Each character can register up to `maxVehiclesPerCharacter` vehicles, plates are unique server-wide, and owners can report a vehicle stolen or recovered.

`maxCharacters` and `maxVehiclesPerCharacter` are set in `config/settings.json`.

**Appearance:** each character has its own look. Creating a character opens the character creator, and **Edit appearance** on the Characters app reopens it (off duty only). Players build a custom male or female character (parents, face shape, hair, facial details, eye colour and clothing), or pick one of the standard GTA peds listed under `creator.standardPeds` in `config/settings.json` (set `creator.allowStandardPeds` to `false` to require custom characters). The look is saved per character, reapplied on every spawn and character switch, and restored when going off duty. Locker uniforms need a custom character; standard peds get the department's `fallbackPedModels` instead.

### 911
Every player has a 911 app to call Police, EMS or Fire from their current position, optionally as an anonymous caller. On-duty units in that department are notified and see the call in their Dispatch app with the caller and description; it has no primary unit, and any unit can attach to respond. The caller is told when units respond and when the call is cleared, and can cancel it while it is open. An attached unit clears the call from the Dispatch app when finished.

Each player can have one open 911 call at a time, with `emergencyCallCooldownSeconds` between calls. A call that nobody attaches to closes after `emergencyCallTimeoutMinutes`.

### Admin
Players with `fiveprs.admin` get an Admin app. It lists online players and can search offline players by name, with buttons to add or remove each player from the Police, EMS and Fire rosters. Changes apply immediately: the player is notified, their department options update, and anyone removed from the department they are on duty in is taken off duty. Every change is written to the audit log.

The roster only takes effect when `fiveprs_restrict_departments` is `true`; the tab shows a warning while it is off.

## Addon API
Other client resources can read the local player's FivePRS state with `exports.fiveprs:getState()`. It returns `name`, `onDuty`, `department` and `rank`. While on duty, it also returns `agency`, `callsign`, `status` and `territory`, plus `callId` and `call` while assigned to a call. The official `fiveprs_presence` addon uses it for Discord rich presence.

## Branding
The terminal nameplate, the notification logo, the server icon and the department icons are loaded from the FivePRS CDN. The URLs live under `branding` in `config/settings.json` and can point to your own images. The department icons under `branding.departments` (`police`, `ems`, `fire` and `civilian`) are shown in the Duty, Dispatch, Characters and 911 apps and in the station menu; a missing image is simply left out. The default terminal wallpapers are served from `https://cdn.fiveprs.org/fiveprs/wallpapers/`.

The server icon is downloaded on start and cached in `data/server_icon.png`, which is used if the download fails. It is only applied when `fiveprs_server_icon` is `true`, no icon has been loaded with `load_server_icon`, and `server.cfg` allows FivePRS to run the command with `add_ace resource.fiveprs command.load_server_icon allow`.

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
| `armory`, `locker`, `garage` | `[x, y, z]` markers for each service. Without `garage`, the first parking spot or the pavement by the nearest road is used. Without `armory` or `locker`, that service is offered at the duty point instead |
| `parking` | Optional vehicle spots `[x, y, z, heading]`; the first free one is used, otherwise the nearest road to the garage |
| `blip` | Optional `{ "sprite": 60, "color": 3 }` to override the department's default blip |

Each service has its own marker for on-duty members of the station's department; Mission Row ships with its armory, locker room and garage set. Pressing `E` (FivePRS: Interact) on a marker opens it. On the duty point it opens the Duty app, or the station menu while on duty. The station menu sits on the right of the screen and is used with the arrow keys or mouse wheel, Enter and Backspace:

- **Armory:** rank kits from `police_loadouts.json` (Recruit, Officer at rank 3, Senior Officer at rank 5, Command at rank 8), each weapon on its own, body armour, ammo refills and returning weapons. A weapon can set a `label` to change its menu name.
- **Locker:** the patrol uniform, the command uniform from rank 8, and your own clothes. Uniforms are freemode clothing; players using a standard GTA ped get `fallbackPedModels` from `police_loadouts.json` (the GTA police officer models by default) instead.
- **Garage:** every model from `police_vehicles.json` your rank unlocks, parked outside, and returning your vehicle when it is near the garage. You have one vehicle at a time.

To get coordinates, stand where you want a point and run `/fprs_coords` for a duty point, or `/fprs_coords armory`, `locker` or `garage`. Sit in a parked vehicle for a parking spot. The values are copied to the clipboard in `stations.json` format and printed to the F8 console.

When going on duty, the Duty app asks where to start:

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
