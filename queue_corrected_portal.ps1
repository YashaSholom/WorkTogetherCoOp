$history = Invoke-RestMethod 'http://127.0.0.1:8190/history'
$prompt = $history.'76fe4894-f70b-42fc-b67a-9a59037b6c50'.prompt[2]

# Scale a four-view contact sheet to a stable canvas, then split it into equal quarters.
$prompt.'364'.inputs.image = 'a6dda907-8e3b-4547-8ea5-97ddd2c555b8.png'
$prompt | Add-Member -NotePropertyName '378' -NotePropertyValue ([pscustomobject]@{
    class_type = 'ImageScale'
    inputs = [pscustomobject]@{
        image = @('364', 0)
        upscale_method = 'lanczos'
        width = 6336
        height = 2688
        crop = 'disabled'
    }
    _meta = [pscustomobject]@{title='Normalize four-view contact sheet'}
})
$cropIds = @('338','341','342','345')
# These boundaries match the actual unequal-width views in the supplied portal sheet:
# front, left-side, back, right-side. Each crop is then independently masked and centred.
$portalRegions = @(
    [pscustomobject]@{x=0;    width=2218},
    [pscustomobject]@{x=2218; width=848},
    [pscustomobject]@{x=3066; width=2136},
    [pscustomobject]@{x=5202; width=1134}
)
for ($i = 0; $i -lt $cropIds.Count; $i++) {
    $prompt.($cropIds[$i]).inputs.image = @('378', 0)
    $prompt.($cropIds[$i]).inputs.crop_region = [pscustomobject]@{x=$portalRegions[$i].x;y=0;width=$portalRegions[$i].width;height=2688}
}

$body = @{ prompt = $prompt; client_id = 'codex-quality-repair-portal' } | ConvertTo-Json -Depth 100
$result = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8190/prompt' -ContentType 'application/json' -Body $body
$result | ConvertTo-Json -Compress
