$source = 'C:\Users\Yasha\Documents\ComfyUI-Trellis2\user\default\workflows\3d_pixal3d_multi_views.json'
$destination = 'C:\Users\Yasha\Documents\ComfyUI-Trellis2\user\default\workflows\3d_pixal3d_portal_gate_contact_sheet.json'
$workflow = Get-Content -Raw -LiteralPath $source | ConvertFrom-Json
$regions = @(
    [pscustomobject]@{x=0;    width=2218},
    [pscustomobject]@{x=2218; width=848},
    [pscustomobject]@{x=3066; width=2136},
    [pscustomobject]@{x=5202; width=1134}
)
$cropIds = @(338,341,342,345)
for($i=0; $i -lt $cropIds.Count; $i++) {
    $node = $workflow.nodes | Where-Object id -eq $cropIds[$i]
    $node.widgets_values[0].x = $regions[$i].x
    $node.widgets_values[0].y = 0
    $node.widgets_values[0].width = $regions[$i].width
    $node.widgets_values[0].height = 2688
}
$workflow | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $destination -Encoding utf8
