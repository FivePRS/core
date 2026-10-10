# Changelog

All notable changes to FivePRS are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and FivePRS follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html). Each release on [GitHub](https://github.com/FivePRS/core/releases) is tagged `v<version>` and uses its section below as the release notes.

## [Unreleased]

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
- Fire and EMS can be joined, but have no callouts or department tools yet; police is the playable department
- Only Mission Row has armory, locker room and garage points
- Back up your database before updating between test builds

[Unreleased]: https://github.com/FivePRS/core/compare/v0.9.0-ptb.1...HEAD
[0.9.0-ptb.1]: https://github.com/FivePRS/core/releases/tag/v0.9.0-ptb.1
