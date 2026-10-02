$singlePath = 'C:\Users\Yasha\Documents\ComfyUI-Trellis2\user\default\workflows\3d_single_image_to_model.json'
$multiPath = 'C:\Users\Yasha\Documents\ComfyUI-Trellis2\user\default\workflows\3d_pixal3d_multi_views.json'

# The single-image preset returns to its known-quality baseline: a native 4096 bake
# with no post-bake sharpening, which was accentuating generated artefacts.
$single = Get-Content -Raw -LiteralPath $singlePath | ConvertFrom-Json
($single.nodes | Where-Object id -eq 288).widgets_values[0] = 4096
$sharpen = $single.nodes | Where-Object id -eq 324
$single.nodes = @($single.nodes | Where-Object id -ne 324)
$single.links = @($single.links | Where-Object { $_[0] -ne 1297 })
$apply = $single.nodes | Where-Object id -eq 210
$baseInput = $apply.inputs | Where-Object name -eq 'base_color'
$baseInput.link = 1270
$single.links += ,@(1270, 164, 0, 210, 1, 'IMAGE')
$single.last_node_id = 323
$single.last_link_id = 1296
$single | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $singlePath -Encoding utf8

# Normalise every four-view contact sheet to the same canvas before splitting it
# into equal panels. This makes the workflow work for both the existing character
# sheet and the user's portal sheet instead of interpreting the full sheet as one image.
$multi = Get-Content -Raw -LiteralPath $multiPath | ConvertFrom-Json
$scaleNode = [pscustomobject]@{
    id = 378; type = 'ImageScale'; pos = @(560, -620); size = @(315, 130); flags = @{}; order = 0; mode = 0
    inputs = @(
        [pscustomobject]@{name='image'; type='IMAGE'; link=1444},
        [pscustomobject]@{name='upscale_method'; type='COMBO'; widget=[pscustomobject]@{name='upscale_method'}; link=$null},
        [pscustomobject]@{name='width'; type='INT'; widget=[pscustomobject]@{name='width'}; link=$null},
        [pscustomobject]@{name='height'; type='INT'; widget=[pscustomobject]@{name='height'}; link=$null},
        [pscustomobject]@{name='crop'; type='COMBO'; widget=[pscustomobject]@{name='crop'}; link=$null}
    )
    outputs = @([pscustomobject]@{name='IMAGE'; type='IMAGE'; links=@(1445,1446,1447,1448); slot_index=0})
    properties = [pscustomobject]@{'Node name for S&R'='ImageScale'}
    widgets_values = @('lanczos',6336,2688,'disabled')
}
$multi.nodes += $scaleNode
$multi.links = @($multi.links | Where-Object { $_[0] -notin @(1423,1424,1425,1426) })
$multi.links += ,@(1444,364,0,378,0,'IMAGE')
$cropIds = @(338,341,342,345)
for($i=0; $i -lt $cropIds.Count; $i++) {
    $node = $multi.nodes | Where-Object id -eq $cropIds[$i]
    $node.widgets_values[0].x = 1584 * $i
    $node.widgets_values[0].y = 0
    $node.widgets_values[0].width = 1584
    $node.widgets_values[0].height = 2688
    $linkId = 1445 + $i
    ($node.inputs | Where-Object name -eq 'image').link = $linkId
    $multi.links += ,@($linkId,378,0,$node.id,0,'IMAGE')
}
$multi.last_node_id = 378
$multi.last_link_id = 1448
$multi | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $multiPath -Encoding utf8
