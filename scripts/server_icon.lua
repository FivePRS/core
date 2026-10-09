local resource = GetCurrentResourceName()
local pngHeader = '\137PNG\r\n\26\n'
local pngTrailer = 'IEND\174\66\96\130'

local function finish(ok, message)
    TriggerEvent('FivePRS:Local:ServerIconResult', ok, message or '')
end

AddEventHandler('FivePRS:Local:ServerIconDownload', function(url, path)
    if GetInvokingResource() ~= nil and GetInvokingResource() ~= resource then return end

    PerformHttpRequest(url, function(status, body, _, err)
        if status ~= 200 then
            finish(false, err and err ~= '' and err or ('HTTP ' .. tostring(status)))
            return
        end

        if type(body) ~= 'string' or body:sub(1, 8) ~= pngHeader or body:sub(-8) ~= pngTrailer then
            finish(false, 'the response was not a complete PNG image')
            return
        end

        if not SaveResourceFile(resource, path, body, #body) then
            finish(false, 'could not write ' .. path)
            return
        end

        finish(true)
    end, 'GET')
end)
