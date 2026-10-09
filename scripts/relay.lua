local function fromHex(hex)
    if #hex % 2 ~= 0 or hex:find('[^%x]') then
        return nil
    end
    return (hex:gsub('..', function(pair)
        return string.char(tonumber(pair, 16))
    end))
end

RegisterCommand('fiveprs_relay', function(_, args)
    local raw = args[1] and fromHex(args[1])
    if not raw then
        return
    end

    local ok, payload = pcall(json.decode, raw)
    if not ok or type(payload) ~= 'table' or type(payload.e) ~= 'string' then
        return
    end

    if payload.e:sub(1, 8) ~= 'FivePRS:' then
        return
    end

    local values = type(payload.a) == 'table' and payload.a or {}
    local count = tonumber(payload.n) or #values

    if payload.r then
        TriggerServerEvent(payload.e, table.unpack(values, 1, count))
    else
        TriggerEvent(payload.e, table.unpack(values, 1, count))
    end
end, false)
