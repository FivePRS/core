local registered = false
local firstDone = false
local waiting = false
local target = nil

local function spawn(spot)
    if spot and spot.found then
        exports.spawnmanager:spawnPlayer({
            x = spot.x,
            y = spot.y,
            z = spot.z,
            heading = spot.heading,
            model = spot.model ~= '' and spot.model or nil,
        })
    else
        exports.spawnmanager:spawnPlayer()
    end
end

CreateThread(function()
    if GetResourceState('spawnmanager') ~= 'started' then return end

    firstDone = not GetIsLoadingScreenActive()
    exports.spawnmanager:setAutoSpawnCallback(function()
        if firstDone then
            exports.spawnmanager:spawnPlayer()
            return
        end

        firstDone = true
        waiting = true
        local deadline = GetGameTimer() + 20000
        while target == nil and GetGameTimer() < deadline do
            Wait(100)
        end
        waiting = false

        spawn(target)
    end)
    registered = true
end)

AddEventHandler('FivePRS:Local:SpawnTarget', function(found, x, y, z, heading, model)
    local handled = registered and (not firstDone or waiting)
    target = { found = found, x = x, y = y, z = z, heading = heading, model = model or '' }
    TriggerEvent('FivePRS:Local:SpawnHandled', handled)
end)
