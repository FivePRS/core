fx_version 'cerulean'
game 'gta5'

name 'FivePRS'
author 'Pixel <https://codemeapixel.dev>'
description 'The Unified Public Response Framework for FiveM.'
repository 'https://github.com/FivePRS/core'
license 'AGPL-3.0-or-later'
version '1.0.0'

lua54 'yes'

server_scripts {
    'scripts/http.lua',
    'server/FivePRS.Server.net.dll',
}

ui_page 'nui/index.html'

client_scripts {
    'scripts/relay.lua',
    'client/FivePRS.Client.net.dll',
    'client/FivePRS.Police.net.dll',
    'plugins/*.net.dll',
    'callouts/*.net.dll',
    'apps/*.net.dll',
}

files {
    'nui/index.html',
    'nui/style.css',
    'nui/dui-image.html',
    'nui/common.js',
    'nui/duty.js',
    'nui/dispatch.js',
    'nui/civilian.js',
    'nui/records.js',
    'nui/emergency.js',
    'nui/admin.js',
    'nui/appview.js',
    'nui/app-sdk.js',
    'nui/menu.js',
    'nui/creator.js',
    'nui/app.js',
    'client/FivePRS.Core.dll',
    'client/Newtonsoft.Json.dll',
    'plugins/*.dll',
    'callouts/*.dll',
    'apps/**/*',
    'config/*.json',
}

convar_policy {
    'fiveprs_db_type',
    'fiveprs_db_connection',
    'fiveprs_max_xp',
    'fiveprs_xp_multiplier',
}
