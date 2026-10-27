fx_version 'cerulean'
game 'gta5'

name 'FivePRS'
author 'Pixel <https://codemeapixel.dev>'
description 'The Unified Public Response Framework for FiveM.'
repository 'https://github.com/FivePRS/core'
license 'AGPL-3.0-or-later'
version '0.9.0-ptb.5'

lua54 'yes'

server_scripts {
    'scripts/http.lua',
    'scripts/db.lua',
    'scripts/framework.lua',
    'server/FivePRS.Server.net.dll',
}

ui_page 'nui/index.html'

client_scripts {
    'scripts/relay.lua',
    'scripts/spawn.lua',
    'scripts/mugshot.lua',
    'client/FivePRS.Client.net.dll',
    'client/FivePRS.Police.net.dll',
    'client/FivePRS.Fire.net.dll',
    'client/FivePRS.EMS.net.dll',
    'plugins/*.net.dll',
    'callouts/*.net.dll',
    'apps/*.net.dll',
}

files {
    'nui/index.html',
    'nui/theme.css',
    'nui/fonts.css',
    'nui/fonts/*.woff2',
    'nui/style.css',
    'nui/cards.css',
    'nui/cards.js',
    'nui/dui-image.html',
    'nui/kit.js',
    'nui/common.js',
    'nui/capture.js',
    'nui/radar.js',
    'nui/radar.css',
    'nui/appview.js',
    'nui/app-sdk.js',
    'nui/menu.js',
    'nui/creator.js',
    'nui/app.js',
    'nui/apps/**/*',
    'client/FivePRS.Core.dll',
    'client/Newtonsoft.Json.dll',
    'plugins/*.dll',
    'callouts/*.dll',
    'apps/**/*',
    'config/*.json',
    'config/**/*.json',
}

convar_policy {
    'fiveprs_db_type',
    'fiveprs_db_connection',
    'fiveprs_max_xp',
    'fiveprs_xp_multiplier',
    'fiveprs_debug',
}
