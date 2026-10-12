local resource = GetCurrentResourceName()
local methods = { query = 'query', insert = 'insert', update = 'update', batch = 'transaction' }

local function respond(id, result, err)
    if err then
        TriggerEvent('FivePRS:Local:DbResponse', id, '', tostring(err))
    else
        TriggerEvent('FivePRS:Local:DbResponse', id, result == nil and 'null' or json.encode(result), '')
    end
end

local function positional(values)
    local keyed = {}
    for index, value in pairs(values or {}) do
        keyed[tostring(index)] = value
    end
    return keyed
end

local function awaitConnection(id)
    CreateThread(function()
        while true do
            local ok, ready = pcall(function() return exports.oxmysql:isReady() end)
            if not ok then return respond(id, nil, ready) end
            if ready then return respond(id, true) end
            Wait(250)
        end
    end)
end

AddEventHandler('FivePRS:Local:DbRequest', function(id, kind, sql, parameters)
    local invoking = GetInvokingResource()
    if invoking ~= nil and invoking ~= resource then return end

    if kind == 'connect' then return awaitConnection(id) end

    local method = methods[kind]
    if not method then return respond(id, nil, ("unknown database request '%s'"):format(kind)) end

    local ok, err = pcall(function()
        local query, values
        if kind == 'batch' then
            query = json.decode(sql)
            for _, statement in ipairs(query) do
                statement.values = positional(statement.values)
            end
            values = {}
        else
            query = sql
            values = positional(json.decode(parameters))
        end

        local oxmysql = exports.oxmysql
        oxmysql[method](oxmysql, query, values, function(result, failure)
            if failure then return respond(id, nil, failure) end
            if kind == 'batch' and result ~= true then
                return respond(id, nil, 'the transaction failed and was rolled back; oxmysql logged the reason above')
            end
            respond(id, result)
        end, resource, true)
    end)

    if not ok then respond(id, nil, err) end
end)
