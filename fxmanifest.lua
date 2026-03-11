fx_version 'cerulean'
game      'gta5'

name 'FivePRS'
author      'Pixel <https://codemeapixel.dev>'
description 'The Unified Public Response Framework for FiveM.'
repository 'https://github.com/FivePRS/core'
license 'AGPL-3.0-or-later'
version     '1.0.0'

server_scripts {
    'server/FivePRS.Core.dll',
    'server/Newtonsoft.Json.dll',
    'server/MySqlConnector.dll',
    'server/Microsoft.Data.Sqlite.dll',
    'server/SQLitePCLRaw.core.dll',
    'server/SQLitePCLRaw.provider.e_sqlite3.dll',
    'server/FivePRS.Server.dll',
}

client_scripts {
    'client/FivePRS.Core.dll',
    'client/Newtonsoft.Json.dll',
    'client/FivePRS.Client.dll',
    'client/FivePRS.Police.dll',
    -- 'client/FivePRS.EMS.dll',
    -- 'client/FivePRS.Fire.dll',
    'plugins/*.dll',
    'callouts/*.dll',
}

-- ── Config / File assets ──────────────────────────────────────────────────────
files {
    'config/settings.json',
    'config/police_vehicles.json',
    'config/police_loadouts.json',
    -- 'nui/index.html',
    -- 'nui/app.js',
    -- 'nui/style.css',
}

convar_policy {
    'fiveprs_db_type',
    'fiveprs_db_connection',
    'fiveprs_max_xp',
    'fiveprs_xp_multiplier',
}
