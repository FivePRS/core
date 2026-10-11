# Changelog

All notable changes to FivePRS are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and FivePRS follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each release on [GitHub](https://github.com/FivePRS/core/releases) is tagged `v<version>` and uses its section below as the release notes.

## [Unreleased]


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

### Changed
- The native SQLite helper is only loaded when SQLite is the database
- MySQL and MariaDB now go through [oxmysql](https://github.com/overextended/oxmysql) instead of a bundled driver. Install oxmysql, set `mysql_connection_string` as `mysql://user:password@host:port/database`, and `ensure oxmysql` before `fiveprs`. `fiveprs_db_connection` is now only for a custom SQLite path
- The server package is smaller: MySqlConnector and the six libraries it needed are no longer shipped

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
