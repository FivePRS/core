AddEventHandler('FivePRS:Local:CaptureScreenshot', function()
    if GetResourceState('screenshot-basic') ~= 'started' then
        TriggerEvent('FivePRS:Local:ScreenshotTaken', '')
        return
    end

    exports['screenshot-basic']:requestScreenshot({ encoding = 'jpg', quality = 0.92 }, function(data)
        TriggerEvent('FivePRS:Local:ScreenshotTaken', data or '')
    end)
end)

RegisterNUICallback('fiveprsPhoto', function(data, cb)
    local characterId = tonumber(data and data.characterId)
    local photo = data and data.photo

    if characterId and type(photo) == 'string' then
        TriggerLatentServerEvent('FivePRS:Server:CharacterPhoto', 50000, characterId, photo)
    end

    cb({})
end)
