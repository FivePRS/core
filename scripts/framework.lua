local resource = GetCurrentResourceName()
local adapter = nil
local detected = false

local function started(name)
    return GetResourceState(name) == 'started'
end

local function normalizeDate(value)
    if type(value) ~= 'string' then return nil end
    local year, month, day = value:match('^(%d%d%d%d)[-/](%d%d?)[-/](%d%d?)')
    if not year then
        day, month, year = value:match('^(%d%d?)[-/.](%d%d?)[-/.](%d%d%d%d)')
    end
    if not year then return nil end
    return ('%04d-%02d-%02d'):format(tonumber(year), tonumber(month), tonumber(day))
end

local function normalizeGender(value)
    local text = tostring(value or ''):lower()
    if text == '1' or text == 'f' or text == 'female' then return 'Female' end
    return 'Male'
end

local function query(sql, params)
    local result = promise.new()
    exports.oxmysql:query(sql, params, function(rows) result:resolve(rows or {}) end)
    return Citizen.Await(result)
end

local function decode(value)
    if type(value) == 'table' then return value end
    if type(value) ~= 'string' or value == '' then return nil end
    local ok, decoded = pcall(json.decode, value)
    return ok and decoded or nil
end

local function cleanLicense(value)
    if type(value) ~= 'string' or value == '' then return nil end
    return (value:gsub('^license2?:', ''):gsub('^char%d+:', ''))
end

local function rowsToVehicles(rows, modelColumn)
    local vehicles = {}
    for _, row in ipairs(rows) do
        local model = modelColumn and row[modelColumn] or nil
        vehicles[#vehicles + 1] = { plate = row.plate, model = type(model) == 'string' and model or nil }
    end
    return vehicles
end

local function qbCharacter(player)
    if not player or not player.PlayerData then return nil end
    local data = player.PlayerData
    local info = data.charinfo or {}
    local job = data.job or {}
    local grade = job.grade
    return {
        id = data.citizenid,
        firstName = info.firstname,
        lastName = info.lastname,
        dateOfBirth = normalizeDate(info.birthdate),
        gender = normalizeGender(info.gender),
        job = job.name,
        grade = tonumber(type(grade) == 'table' and grade.level or grade) or 0,
        gradeName = type(grade) == 'table' and grade.name or nil,
        onDuty = job.onduty == true,
        tracksDuty = true,
    }
end

local function qbAdapter(name, resourceName, getPlayer)
    return {
        name = name,
        family = 'qb',
        resource = resourceName,
        vehicles = function(id)
            return rowsToVehicles(query('SELECT plate, vehicle FROM player_vehicles WHERE citizenid = ?', { id }), 'vehicle')
        end,
        vehicle = function(plate)
            local row = query("SELECT pv.plate, pv.vehicle, pv.citizenid, p.license, p.charinfo FROM player_vehicles pv " ..
                "LEFT JOIN players p ON p.citizenid = pv.citizenid WHERE REPLACE(UPPER(pv.plate), ' ', '') = ? LIMIT 1", { plate })[1]
            if not row then return nil end
            local info = decode(row.charinfo) or {}
            return {
                plate = row.plate,
                model = row.vehicle,
                owner = {
                    id = row.citizenid,
                    license = cleanLicense(row.license),
                    firstName = info.firstname,
                    lastName = info.lastname,
                    dateOfBirth = normalizeDate(info.birthdate),
                    gender = normalizeGender(info.gender),
                },
            }
        end,
        character = function(source)
            return qbCharacter(getPlayer(source))
        end,
        setDuty = function(source, onDuty)
            local player = getPlayer(source)
            if player and player.Functions and player.Functions.SetJobDuty then
                player.Functions.SetJobDuty(onDuty)
                return true
            end
            return false
        end,
        removeMoney = function(source, account, amount, reason)
            local player = getPlayer(source)
            return player ~= nil and player.Functions.RemoveMoney(account, amount, reason) == true
        end,
        addMoney = function(source, account, amount, reason)
            local player = getPlayer(source)
            return player ~= nil and player.Functions.AddMoney(account, amount, reason) == true
        end,
        listen = function(notify)
            AddEventHandler('QBCore:Server:PlayerLoaded', function(player)
                if player and player.PlayerData then notify(player.PlayerData.source, 'loaded') end
            end)
            AddEventHandler('QBCore:Server:OnPlayerUnload', function(source) notify(source, 'unloaded') end)
            AddEventHandler('QBCore:Server:OnJobUpdate', function(source) notify(source, 'job') end)
            AddEventHandler('QBCore:Server:SetDuty', function(source) notify(source, 'duty') end)
        end,
    }
end

local builders = {}

builders.qbox = function()
    return qbAdapter('qbox', 'qbx_core', function(source)
        return exports.qbx_core:GetPlayer(source)
    end)
end

builders.qbcore = function()
    local core = exports['qb-core']:GetCoreObject()
    return qbAdapter('qbcore', 'qb-core', function(source)
        return core.Functions.GetPlayer(source)
    end)
end

builders.esx = function()
    local ESX = exports.es_extended:getSharedObject()
    return {
        name = 'esx',
        family = 'esx',
        resource = 'es_extended',
        vehicles = function(id)
            return rowsToVehicles(query('SELECT plate FROM owned_vehicles WHERE owner = ?', { id }))
        end,
        vehicle = function(plate)
            local row = query("SELECT ov.plate, ov.owner, u.firstname, u.lastname, u.dateofbirth, u.sex FROM owned_vehicles ov " ..
                "LEFT JOIN users u ON u.identifier = ov.owner WHERE REPLACE(UPPER(ov.plate), ' ', '') = ? LIMIT 1", { plate })[1]
            if not row then return nil end
            return {
                plate = row.plate,
                owner = {
                    id = row.owner,
                    license = cleanLicense(row.owner),
                    firstName = row.firstname,
                    lastName = row.lastname,
                    dateOfBirth = normalizeDate(row.dateofbirth),
                    gender = normalizeGender(row.sex),
                },
            }
        end,
        character = function(source)
            local player = ESX.GetPlayerFromId(source)
            if not player then return nil end
            local job = player.getJob and player.getJob() or player.job or {}
            local first, last = player.get('firstName'), player.get('lastName')
            if not first and player.getName then
                first, last = tostring(player.getName()):match('^(%S+)%s*(.*)$')
            end
            return {
                id = player.identifier,
                firstName = first,
                lastName = last,
                dateOfBirth = normalizeDate(player.get('dateofbirth')),
                gender = normalizeGender(player.get('sex')),
                job = job.name,
                grade = tonumber(job.grade) or 0,
                gradeName = job.grade_label or job.grade_name,
                onDuty = nil,
                tracksDuty = false,
            }
        end,
        setDuty = function() return false end,
        removeMoney = function(source, account, amount, reason)
            local player = ESX.GetPlayerFromId(source)
            if not player then return false end
            local balance = player.getAccount(account)
            if not balance or balance.money < amount then return false end
            player.removeAccountMoney(account, amount, reason)
            return true
        end,
        addMoney = function(source, account, amount, reason)
            local player = ESX.GetPlayerFromId(source)
            if not player then return false end
            player.addAccountMoney(account, amount, reason)
            return true
        end,
        listen = function(notify)
            AddEventHandler('esx:playerLoaded', function(source) notify(source, 'loaded') end)
            AddEventHandler('esx:playerLogout', function(source) notify(source, 'unloaded') end)
            AddEventHandler('esx:setJob', function(source) notify(source, 'job') end)
        end,
    }
end

builders.ox = function()
    local function getPlayer(source)
        return exports.ox_core:GetPlayer(source)
    end

    return {
        name = 'ox',
        family = 'ox',
        resource = 'ox_core',
        vehicles = function(id)
            return rowsToVehicles(query('SELECT plate, model FROM vehicles WHERE owner = ?', { tonumber(id) or id }), 'model')
        end,
        vehicle = function(plate)
            local row = query("SELECT v.plate, v.model, v.owner, c.firstName, c.lastName, c.dateOfBirth, c.gender FROM vehicles v " ..
                "LEFT JOIN characters c ON c.charId = v.owner WHERE REPLACE(UPPER(v.plate), ' ', '') = ? LIMIT 1", { plate })[1]
            if not row or not row.owner then return nil end
            return {
                plate = row.plate,
                model = row.model,
                owner = {
                    id = tostring(row.owner),
                    firstName = row.firstName,
                    lastName = row.lastName,
                    dateOfBirth = normalizeDate(row.dateOfBirth),
                    gender = normalizeGender(row.gender),
                },
            }
        end,
        character = function(source, jobs)
            local player = getPlayer(source)
            if not player or not player.charId then return nil end
            local get = function(key) return player.get and player.get(key) or player[key] end
            local job, grade = nil, 0
            for _, name in ipairs(jobs or {}) do
                local level = player.getGroup and player.getGroup(name)
                if level and level > 0 then
                    job, grade = name, level
                    break
                end
            end
            return {
                id = tostring(player.charId),
                firstName = get('firstName'),
                lastName = get('lastName'),
                dateOfBirth = normalizeDate(get('dateOfBirth')),
                gender = normalizeGender(get('gender')),
                job = job,
                grade = math.max(grade - 1, 0),
                onDuty = nil,
                tracksDuty = false,
            }
        end,
        setDuty = function() return false end,
        removeMoney = function(source, _, amount, reason)
            local player = getPlayer(source)
            if not player or not player.charId then return false end
            local account = exports.ox_core:GetCharacterAccount(player.charId)
            if not account then return false end
            local result = account.removeBalance({ amount = amount, message = reason })
            return result == true or (type(result) == 'table' and result.success == true)
        end,
        addMoney = function(source, _, amount, reason)
            local player = getPlayer(source)
            if not player or not player.charId then return false end
            local account = exports.ox_core:GetCharacterAccount(player.charId)
            if not account then return false end
            local result = account.addBalance({ amount = amount, message = reason })
            return result == true or (type(result) == 'table' and result.success == true)
        end,
        listen = function(notify)
            AddEventHandler('ox:playerLoaded', function(source) notify(source, 'loaded') end)
            AddEventHandler('ox:playerLogout', function(source) notify(source, 'unloaded') end)
            AddEventHandler('ox:setGroup', function(source) notify(source, 'job') end)
        end,
    }
end

builders.nd = function()
    local function getPlayer(source)
        return exports['ND_Core']:getPlayer(source)
    end

    return {
        name = 'nd',
        family = 'nd',
        resource = 'ND_Core',
        vehicles = function(id)
            return rowsToVehicles(query('SELECT plate FROM nd_vehicles WHERE owner = ?', { tonumber(id) or id }))
        end,
        vehicle = function(plate)
            local row = query("SELECT v.plate, v.owner, c.identifier, c.firstname, c.lastname, c.dob, c.gender FROM nd_vehicles v " ..
                "LEFT JOIN nd_characters c ON c.charid = v.owner WHERE REPLACE(UPPER(v.plate), ' ', '') = ? LIMIT 1", { plate })[1]
            if not row or not row.owner then return nil end
            return {
                plate = row.plate,
                owner = {
                    id = tostring(row.owner),
                    license = cleanLicense(row.identifier),
                    firstName = row.firstname,
                    lastName = row.lastname,
                    dateOfBirth = normalizeDate(row.dob),
                    gender = normalizeGender(row.gender),
                },
            }
        end,
        character = function(source)
            local player = getPlayer(source)
            if not player then return nil end
            local info = player.jobInfo or {}
            return {
                id = tostring(player.id),
                firstName = player.firstname,
                lastName = player.lastname,
                dateOfBirth = normalizeDate(player.dob),
                gender = normalizeGender(player.gender),
                job = player.job,
                grade = tonumber(info.rank) or 0,
                gradeName = info.rankName,
                onDuty = nil,
                tracksDuty = false,
            }
        end,
        setDuty = function() return false end,
        removeMoney = function(source, account, amount, reason)
            local player = getPlayer(source)
            return player ~= nil and player.deductMoney(account, amount, reason) == true
        end,
        addMoney = function(source, account, amount, reason)
            local player = getPlayer(source)
            return player ~= nil and player.addMoney(account, amount, reason) == true
        end,
        listen = function(notify)
            AddEventHandler('ND:characterLoaded', function(character)
                if character and character.source then notify(character.source, 'loaded') end
            end)
            AddEventHandler('ND:characterUnloaded', function(source) notify(source, 'unloaded') end)
            AddEventHandler('ND:updateCharacter', function(character)
                if character and character.source then notify(character.source, 'job') end
            end)
        end,
    }
end

local order = {
    { name = 'qbox', resource = 'qbx_core' },
    { name = 'qbcore', resource = 'qb-core' },
    { name = 'esx', resource = 'es_extended' },
    { name = 'ox', resource = 'ox_core' },
    { name = 'nd', resource = 'ND_Core' },
}

local function waitFor(resourceName, timeoutMs)
    local deadline = GetGameTimer() + timeoutMs
    while not started(resourceName) and GetGameTimer() < deadline do
        Wait(250)
    end
    return started(resourceName)
end

local function detect(preferred)
    if detected then return adapter end
    detected = true

    if preferred == 'standalone' then return nil end

    local candidates = {}
    for _, entry in ipairs(order) do
        if preferred == 'auto' or preferred == entry.name then
            candidates[#candidates + 1] = entry
        end
    end

    for _, entry in ipairs(candidates) do
        if started(entry.resource) or (preferred ~= 'auto' and waitFor(entry.resource, 30000)) then
            local ok, built = pcall(builders[entry.name])
            if ok and built then
                adapter = built
                adapter.listen(function(source, kind)
                    TriggerEvent('FivePRS:Local:FrameworkChanged', tonumber(source) or 0, kind)
                end)
                return adapter
            end
            print(('^5[FivePRS]^1 Could not connect to %s: %s^7'):format(entry.resource, tostring(built)))
        end
    end
    return nil
end

local function respond(id, result, err)
    if err then
        TriggerEvent('FivePRS:Local:FrameworkResponse', id, '', tostring(err))
    else
        TriggerEvent('FivePRS:Local:FrameworkResponse', id, result == nil and 'null' or json.encode(result), '')
    end
end

local handlers = {
    detect = function(args)
        local found = detect(args.preferred or 'auto')
        return found and { name = found.name, family = found.family, resource = found.resource } or nil
    end,
    character = function(args)
        return adapter and adapter.character(args.source, args.jobs) or nil
    end,
    vehicles = function(args)
        return adapter and adapter.vehicles(args.id) or {}
    end,
    vehicle = function(args)
        return adapter and adapter.vehicle(args.plate) or nil
    end,
    setDuty = function(args)
        return adapter ~= nil and adapter.setDuty(args.source, args.onDuty == true) == true
    end,
    removeMoney = function(args)
        return adapter ~= nil and adapter.removeMoney(args.source, args.account or 'bank', args.amount, args.reason) == true
    end,
    addMoney = function(args)
        return adapter ~= nil and adapter.addMoney(args.source, args.account or 'bank', args.amount, args.reason) == true
    end,
}

AddEventHandler('FivePRS:Local:FrameworkRequest', function(id, method, argsJson)
    local invoking = GetInvokingResource()
    if invoking ~= nil and invoking ~= resource then return end

    local handler = handlers[method]
    if not handler then return respond(id, nil, ("unknown framework request '%s'"):format(method)) end

    CreateThread(function()
        local ok, result = pcall(handler, json.decode(argsJson or '{}') or {})
        if ok then respond(id, result) else respond(id, nil, result) end
    end)
end)
