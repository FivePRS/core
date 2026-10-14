RegisterNUICallback('fiveprsPhoto', function(data, cb)
    local characterId = tonumber(data and data.characterId)
    local photo = data and data.photo

    if characterId and type(photo) == 'string' then
        TriggerLatentServerEvent('FivePRS:Server:CharacterPhoto', 50000, characterId, photo)
    end

    cb({})
end)
