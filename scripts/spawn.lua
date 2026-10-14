local REQUEST_INTERVAL = 5000
local TARGET_TIMEOUT = 60000

local registered = false
local ownsFirstSpawn = false
local firstPending = false
local usedTarget = false
local target = nil

local function debug(message)
    if GetConvar('fiveprs_debug', 'false') == 'true' then
        print(('^5[FivePRS]^6 Spawn: %s^7'):format(message))
    end
end

local function setTarget(found, x, y, z, heading, model)
    target = { found = found, x = x, y = y, z = z, heading = heading, model = model or '' }
    debug(('target received (found %s, model "%s")'):format(tostring(found), model or ''))
end

local function spawn(spot)
    if spot and spot.found then
        debug(('spawning at the last position as "%s"'):format(spot.model))
        usedTarget = true
        exports.spawnmanager:spawnPlayer({
            x = spot.x,
            y = spot.y,
            z = spot.z,
            heading = spot.heading,
            model = spot.model ~= '' and spot.model or nil,
        })
    else
        debug(spot and 'no saved position, using the map spawn point' or 'no target from the server, using the map spawn point')
        exports.spawnmanager:spawnPlayer()
    end
end

RegisterNetEvent('FivePRS:Client:LastPosition', setTarget)

CreateThread(function()
    if GetResourceState('spawnmanager') ~= 'started' then
        debug('spawnmanager is not running, leaving spawning to another script')
        return
    end

    ownsFirstSpawn = GetIsLoadingScreenActive()
    debug(ownsFirstSpawn and 'handling the first spawn' or 'joined mid-session, not handling the first spawn')
    firstPending = ownsFirstSpawn
    exports.spawnmanager:setAutoSpawnCallback(function()
        if not firstPending then
            exports.spawnmanager:spawnPlayer()
            return
        end

        local deadline = GetGameTimer() + TARGET_TIMEOUT
        local nextRequest = 0
        while target == nil and GetGameTimer() < deadline do
            if GetGameTimer() >= nextRequest then
                TriggerServerEvent('FivePRS:Server:LastPositionRequest')
                nextRequest = GetGameTimer() + REQUEST_INTERVAL
            end
            Wait(100)
        end

        spawn(target)
        firstPending = false
    end)
    registered = true
end)

AddEventHandler('FivePRS:Local:SpawnTarget', function(found, x, y, z, heading, model)
    if target == nil then setTarget(found, x, y, z, heading, model) end
    TriggerEvent('FivePRS:Local:SpawnHandled', registered and ownsFirstSpawn and (firstPending or usedTarget))
end)
