local resource = GetCurrentResourceName()

local checks = {
    png = function(body) return body:sub(1, 8) == '\137PNG\r\n\26\n' and body:sub(-8) == 'IEND\174\66\96\130' end,
    zip = function(body) return body:sub(1, 4) == 'PK\3\4' end,
}

local function respond(id, status, body, err)
    TriggerEvent('FivePRS:Local:HttpResponse', id, status, body or '', err or '')
end

AddEventHandler('FivePRS:Local:HttpRequest', function(id, url, savePath, accept, check)
    if GetInvokingResource() ~= nil and GetInvokingResource() ~= resource then return end

    local headers = { ['User-Agent'] = 'FivePRS' }
    if accept ~= '' then headers['Accept'] = accept end

    PerformHttpRequest(url, function(status, body, _, err)
        if status < 200 or status >= 300 or type(body) ~= 'string' then
            respond(id, status, '', err and err ~= '' and err or ('HTTP ' .. tostring(status)))
            return
        end

        if checks[check] and not checks[check](body) then
            respond(id, status, '', 'the download was incomplete or not the expected file type')
            return
        end

        if savePath == '' then
            respond(id, status, body)
            return
        end

        if not SaveResourceFile(resource, savePath, body, #body) then
            respond(id, status, '', 'could not write ' .. savePath)
            return
        end

        respond(id, status, '')
    end, 'GET', '', headers)
end)
