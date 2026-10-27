# Changelog

All notable changes to FivePRS are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and FivePRS follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each release on [GitHub](https://github.com/FivePRS/core/releases) is tagged `v<version>` and uses its section below as the release notes.

## [Unreleased]

### Added
- Framework support for Qbox, QBCore, ESX Legacy, ox_core and ND_Core. `config/framework.json` maps framework jobs to departments and agencies, and job grades to ranks. With a framework, the character's job decides who can go on duty and at what rank, players show by their character's name, and duty stays in step with Qbox and QBCore. Framework characters get FivePRS records, so citations, warrants, licenses and the MDT work with them, their owned vehicles are found by plate, and citation fines come out of the character's bank. Spawning and clothing are left to the framework. FivePRS still runs standalone when no framework is found
- `xpEnabled` in `config/settings.json` turns XP off, so ranks are only set by admins. The terminal hides XP when it's off
- Patrol car radar with front and rear antennas that read same-direction or oncoming traffic, separate front (`J`) and rear (`U`) locks, closing and away indicators, an error reading while turning or airborne, a doppler tone and beeps, and a digital display. The operator menu (`F9`) sets each antenna, a fast limit with optional auto-lock, volumes, and the display's size and position, and remembers them. A plate reader checks the vehicles in front and behind against records and flags stolen vehicles, wanted owners, suspended licenses and unregistered plates. A handheld lidar gun measures speed and distance on foot. Settings are under `radar` in `config/settings.json`
- **As I am** in the Duty app starts your shift with your current look and no vehicle. It's the default, and the Duty app remembers the start you used last

### Fixed
- Pulled-over vehicles no longer drive off in a loop to a point on another road before stopping. They pull to the side of their own road, or stop where they are
- Starting a shift at a **Station** now gives you your uniform, loadout and a vehicle parked outside, as the Duty app says. Before, it only teleported you there

## [0.9.0-ptb.5] - 2026-10-14

### Added
- A redesigned terminal: new home screen with the time, your location, an incoming call you can accept from home, and unit, call and shift counts. Every app has a new layout, with bundled fonts so it looks the same offline
- Character photos: take an in-game mugshot, with no extra resource needed, or use an https image link. Photos show on the character, on license cards and in police Records
- Licenses are shown as ID cards with the photo, a license number, class, issue and expiry dates and a status stamp. Show a card to the person next to you from the Licenses app. Each license type's card title, issuer, colours and background art are set in `licenses.json`, along with the state name used on cards, plates and state IDs
- Records has a warrant banner, the person's photo and quick flags, and an offence picker that fills in the fine from `citations.json`
- Records for police in the field: a Current stop card to run the stopped vehicle's plate and open the driver's record, Vehicle ahead to run the plate in front of you, and Person ahead to identify the person you're facing. Searching the plate of a nearby car finds its owner even if nobody has run it before
- Records for Fire and EMS: identity, photo and medical history, with a caution banner when the person has an active warrant. They add medical entries; licenses, vehicles and the criminal record stay with police. Some people on the street now have medical conditions on file
- Patient care and incident reports: handing a patient over at the hospital, or closing a fire call, drafts a report with the address and the people or plate involved. Everyone on the call can edit and file it from the Reports tab in Records, and drafts nobody files are filed after 30 minutes. Filing a patient report adds it to the patient's medical history. Filed reports can be searched by address, call number, plate and date
- Dispatch can request backup from your active call, and shows distances to calls
- 911 shows your call's progress: received, units responding, on scene
- Duty shows your next rank and your calls, arrests and hours on duty, which are now tracked
- Admin has sections for players, the roster, active calls, the audit log and server info, with set rank, grant XP, set department, revive, teleport and force off duty for each player
- Settings has text size, a 24-hour clock, reduced motion, the AI callouts switch and your current key bindings
- Named ranks for each department in `config/departments/<department>/ranks.json`: set each rank's title, the XP to reach it and the tier of weapons, uniforms and vehicles it unlocks. Agencies can give ranks their own titles (BCSO ships with Deputy and Sheriff). The defaults keep the existing progression, and ranks are shown by name in the terminal, the station menu, notifications and the loading screen
- Errors are saved in full to `data/logs`, one file per day, kept for 14 days. The console shows one line with the cause
- Callouts and plugins can log through FivePRS: `Log` in callouts, `Logger.For(name)` anywhere else

### Changed
- The `config/` folder is organised into `departments/`, `world/` and `civilian/`. On start, FivePRS moves files from the old layout into place and creates any missing `ranks.json`
- The console says "Created a new database" when it starts on an empty database, instead of listing the first migration
- Console messages are shorter and coloured by type: green for success, yellow for warnings, red for errors. Debug messages only show with `fiveprs_debug` on, which now also works on clients when set with `setr`
- Every built-in terminal app (Duty, Dispatch, Records, Characters, Licenses, Vehicles, 911 and Admin) is now a page app on the same framework addons use, so they all open, update and badge the same way
- Mugshots no longer need the `screenshot-basic` resource
- The shift timer on the home screen counts seconds and keeps counting through a terminal reload
- Database schema 3 adds the reports table. SQLite databases are backed up before updating

### Fixed
- Your character's look is applied as you spawn, whichever spawn script you use, instead of leaving you as the map's ped
- Joining spawns you at your last location as your character even when the client is slow to start, instead of spawning at the map's spawn point and teleporting
- Joining after the loading screen closes early no longer spawns you at the map's spawn point instead of your last location
- The welcome message shows once after you spawn, instead of during the loading screen and again when switching characters
- The FivePRS server icon no longer reports that it failed when `load_server_icon` is just slow to apply
- Admin commands show success in green and problems in red, in game and in the console, and reject missing or extra arguments with their usage
- `fprs_setrank` only accepts ranks that exist in the player's department, and `fprs_addxp` up to 1,000,000 XP at a time
- `fprs_setdept` only accepts department names, and lists them when the name is wrong
- `fprs_revive` says when the player isn't down instead of reporting a revive
- `/backup` rejects options it doesn't know instead of sending a Code 3 request
- `/er_status` only accepts `available` or `busy`, and `/er_status` and `/er_attach` say when you're off duty
- `/fprs_coords` rejects point names it doesn't know instead of copying a different format
- `/er_profile` says when your profile hasn't loaded yet

## [0.9.0-ptb.4] - 2026-10-11

### Added
- Request backup at Code 2 or Code 3 from the stop menu, with `/backup`, or with a key you bind. Choose any unit (players first, then an NPC unit if nobody attaches), an NPC unit straight away, or players only. On a call, backup is requested for that call; otherwise a Backup Request call is created at your position. Responding units get a notification and a flashing map marker. Settings are under `backup` in `config/settings.json`
- The station locker has Edit appearance: change your character's look while on duty, then your uniform goes back on
- Creating a character while on duty explains how to set up their appearance, and the character creator opens when you next go off duty. The Characters app says why Edit appearance is unavailable on duty
- Uniforms can give female characters their own clothing under `female`, since male and female freemode characters number their clothing differently. The default police uniforms include a female version
- On start, FivePRS removes server DLLs left over from older versions (MySqlConnector and the libraries it used)

### Fixed
- The default police uniform no longer hides the officer's arms
- `/fprs_coords` works again instead of failing with `BadImageFormatException`
- Joining puts you straight at your last location instead of spawning at the map's spawn point and then teleporting (with spawnmanager; other spawn scripts still get the teleport)
- You spawn as your last used character. A character with no saved look yet spawns as a plain male or female character, with a reminder to set up their appearance, instead of the map's ped

### Changed
- The native SQLite helper is only loaded when SQLite is the database
- MySQL and MariaDB now go through [oxmysql](https://github.com/overextended/oxmysql) instead of a bundled driver. Install oxmysql, set `mysql_connection_string` as `mysql://user:password@host:port/database`, and `ensure oxmysql` before `fiveprs`. `fiveprs_db_connection` is now only for a custom SQLite path
- The server package is smaller: MySqlConnector and the six libraries it needed are no longer shipped
- Updated duty point for the Mission Row station to be infront of the front desk.
- Updated vehicle list for ranking police officers (LSPD)

### Known issues
- GTA V Enhanced servers aren't supported yet. Enhanced runs C# resources on .NET 10 in a sandbox that FivePRS doesn't run in yet; support is coming in a later release. Use a GTA V Legacy server

## [0.9.0-ptb.3] - 2026-10-10

### Added
- Fire & Rescue (LSFD): go on duty at fire stations with turnout gear, an extinguisher and a fire engine
- Fire callouts: Vehicle Fire and Brush Fire, with real fires that spread until they're put out
- EMS (SAMS): go on duty at hospitals with a medic uniform and an ambulance
- EMS callouts: Medical Emergency and Traffic Collision, where you treat the patient, load them into the ambulance and transport them to the nearest hospital
- Death and revive: downed players wait for EMS, who get an automatic 911 call and can revive them for XP; after a timer, players can respawn at a hospital. `fprs_revive` for admins; settings under `death`
- `config/fire_loadouts.json`, `fire_vehicles.json`, `ems_loadouts.json` and `ems_vehicles.json` configure each department's equipment and vehicles
- Traffic stops: pull over any AI-driven vehicle with your lights on, then ask for ID, run the plate, search, warn, cite, serve warrants, arrest or release
- AI drivers and their vehicles get generated identities that are saved to Records the first time they're checked
- `config/citations.json` sets the offences and fines offered during a traffic stop
- The server publishes its FivePRS version as the `fiveprs_version` server variable, visible in the server list and `info.json`
- Wanted levels and GTA's own AI police, ambulance and fire response are turned off by default, configurable under `world` in `config/settings.json`, with an option to stop ambient AI police spawning
- Pedestrian stops: face anyone on foot and press `E` to stop them, with the same ID, search, citation and arrest actions

### Fixed
- FivePRS no longer fails after the database starts (`Argument at index 0 was null`): it reads its resource name once at startup, and database calls hand control back to the server's main thread
- Setting `fiveprs_db_connection` to a MySQL connection string while `fiveprs_db_type` is `sqlite` (or the reverse, an empty connection with `mysql`) now explains the mismatch
- MySQL and MariaDB connections no longer fail with `NotImplementedException` when the database offers TLS; FivePRS connects without TLS, which FiveM's runtime can't do
- A resource folder the server can't write to now logs a clear message instead of an unhandled exception
- FivePRS now starts on GTA V Enhanced servers, which reject assemblies that load native code. Enhanced servers need a MySQL or MariaDB database, since SQLite needs a native library

### Changed
- Police, Fire and EMS share one department module base; police gear and vehicle config types are now `DepartmentLoadoutsConfig` and `DepartmentVehiclesConfig`
- Server console errors are one line with the cause; set `fiveprs_debug true` for full details
- Common database problems (missing database, wrong login, no access, server unreachable) are explained in plain language with the fix

## [0.9.0-ptb.2] - 2026-10-10

### Added
- `fiveprs_diag` prints the version, platform, database, OneSync, addons and warnings for bug reports
- A startup summary with the version, platform and database, and warnings for problems such as OneSync being off
- The terminal's Settings app shows the FivePRS version
- Release downloads include the version in their name, like `fiveprs-0.9.0-ptb.2.zip`

### Fixed
- The server console no longer shows `Could not load assembly Windows` stack traces on start with SQLite

## [0.9.0-ptb.1] - 2026-10-10

The first public test build.

### Added
- Police, fire and EMS departments with agencies, ranks, XP and callsigns, all defined in config
- Jurisdictions and territories, so calls and units are matched to the right agency
- Server-side dispatch with AI callouts, player 911 calls and a callout API for DLL callout packs
- Records: people, vehicles, warrants, citations and arrests
- Civilian characters with licenses and registered vehicles
- Character creator for freemode characters, saved per character
- The FivePRS terminal: a tablet with a home screen, apps and wallpapers, including server defaults and custom wallpaper URLs that servers can turn off
- Terminal apps from DLLs in `apps/` or from other resources through exports, with a page SDK for HTML apps
- Settings app, including silencing AI callouts while on duty
- Admin app with the department roster
- Stations on the map with going on duty at a station or in the field, and teleporting to a station
- Armory, locker room and garage points at stations, with uniforms, loadouts and stock police vehicles
- `fprs_coords` for copying coordinates, headings and station entries
- Players return to their last location with their last character when they rejoin
- Update checks against GitHub releases, admin notifications and `fiveprs_update` to download and unpack a release
- Server icon loading from a URL
- Notifications with the FivePRS logo and sender
- Plugin loading from `plugins/` and callout packs from `callouts/`
- Audit log of admin and record actions
- SQLite out of the box, and MySQL support
- Versioned database migrations that update the database on start, with an automatic backup first on SQLite
- Windows and Linux server support
- Per-player rate limits for dispatch, 911 calls, records, civilian actions and admin requests, configurable under `rateLimits`
- Validation of callout catalogs sent by clients, with XP rewards capped at `fiveprs_max_xp`

### Known issues
- Fire and EMS aren't available yet; police is the only department you can join
- Only Mission Row has armory, locker room and garage points
- Back up your database before updating between test builds

[Unreleased]: https://github.com/FivePRS/core/compare/v0.9.0-ptb.3...HEAD
[0.9.0-ptb.3]: https://github.com/FivePRS/core/compare/v0.9.0-ptb.3...v0.9.0-ptb.4
[0.9.0-ptb.3]: https://github.com/FivePRS/core/compare/v0.9.0-ptb.2...v0.9.0-ptb.3
[0.9.0-ptb.2]: https://github.com/FivePRS/core/compare/v0.9.0-ptb.1...v0.9.0-ptb.2
[0.9.0-ptb.1]: https://github.com/FivePRS/core/releases/tag/v0.9.0-ptb.1
