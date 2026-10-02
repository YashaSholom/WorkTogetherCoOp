$workflowPath = 'C:\Users\Yasha\Documents\ComfyUI-Trellis2\user\default\workflows\3d_pixal3d_multi_views.json'
$workflow = Get-Content -Raw -LiteralPath $workflowPath | ConvertFrom-Json
$objectInfo = Invoke-RestMethod 'http://127.0.0.1:8190/object_info'

# Convert the saved ComfyUI canvas representation to an executable API prompt.
$links = @{}
foreach ($link in $workflow.links) { $links[[string]$link[0]] = $link }
$prompt = [ordered]@{}
$skipNodeTypes = @('Note', 'MarkdownNote', 'Preview3DAdvanced')
foreach ($node in $workflow.nodes) {
    if ($node.type -in $skipNodeTypes) { continue }
    $inputs = [ordered]@{}
    $nodeInputByName = @{}
    foreach ($input in @($node.inputs)) { $nodeInputByName[$input.name] = $input }
    $schema = $objectInfo.($node.type).input
    $schemaFields = @()
    if ($schema.required) { $schemaFields += @($schema.required.psobject.Properties) }
    if ($schema.optional) { $schemaFields += @($schema.optional.psobject.Properties) }
    $widgetIndex = 0
    $widgetValues = @($node.widgets_values)
    foreach ($field in $schemaFields) {
        $input = $nodeInputByName[$field.Name]
        if ($input -and $null -ne $input.link) {
            $link = $links[[string]$input.link]
            $inputs[$field.Name] = @([string]$link[1], [int]$link[2])
            continue
        }
        $definition = $field.Value
        $inputType = $definition[0]
        $config = $definition[1]
        $isWidget = $inputType -is [System.Collections.IEnumerable] -and $inputType -isnot [string]
        $isWidget = $isWidget -or ($config.PSObject.Properties.Name -contains 'default') -or $field.Name -eq 'viewport_state'
        if ($isWidget -and $widgetIndex -lt $widgetValues.Count) {
            $inputs[$field.Name] = $widgetValues[$widgetIndex]
            $widgetIndex++
            if ($config.PSObject.Properties.Name -contains 'control_after_generate') { $widgetIndex++ }
        }
    }
    $prompt[[string]$node.id] = [ordered]@{
        class_type = $node.type
        inputs = $inputs
        _meta = [ordered]@{ title = if ($node.title) { $node.title } else { $node.type } }
    }
}

# Use the four supplied views directly. No contact-sheet split nodes are involved.
$viewFiles = [ordered]@{
    '338' = 'astronaut_front.png'
    '341' = 'astronaut_left.png'
    '342' = 'astronaut_back.png'
    '345' = 'astronaut_right.png'
}
foreach ($nodeId in $viewFiles.Keys) {
    $prompt[$nodeId] = [ordered]@{
        class_type = 'LoadImage'
        inputs = [ordered]@{ image = $viewFiles[$nodeId] }
        _meta = [ordered]@{ title = "Astronaut view $nodeId" }
    }
}

# Preserve native texture detail; do not apply the experimental image-sharpen stage.
$prompt.Remove('377')
$prompt['210'].inputs.base_color = @('164', 0)
$prompt['288'].inputs.value = 6144
$prompt['193'].inputs.bg_removal_name = 'birefnet.safetensors'
$savedViews = [ordered]@{'340'='front_view';'344'='left_view';'343'='back_view';'346'='right_view'}
foreach($nodeId in $savedViews.Keys) {
    $prompt[$nodeId].inputs.filename_prefix = $savedViews[$nodeId]
    $prompt[$nodeId].inputs.format = 'png'
    $prompt[$nodeId].inputs.'format.bit_depth' = '8-bit'
    $prompt[$nodeId].inputs.'format.input_color_space' = 'sRGB'
}
$prompt['196'].inputs = [ordered]@{mesh=@('238',0);segmenter='pec';resolution=@('288',0);padding=1;weld_distance=0.0002}
$prompt['186'].inputs.placement_mode = 'midpoint'
$prompt['241'].inputs = [ordered]@{
    mesh=@('202',0); resolution=768; sign_mode='udf'; 'sign_mode.qef'=$false
    'sign_mode.drop_inverted_components'=$false; 'sign_mode.drop_enclosed_components'=$false
    band=1.0; project_back=0.0; fix_poles=$false; smooth_iters=20
    drop_small_components=0.01; precluster_max_verts=20000000
}
$prompt['372'].inputs.viewport_state = [ordered]@{
    image=''; mask=''; normal=''; recording=''
    camera_info=[ordered]@{position=[ordered]@{x=9.5;y=9.5;z=9.5};target=[ordered]@{x=0;y=0;z=0};zoom=1;cameraType='perspective';quaternion=[ordered]@{x=-0.28;y=0.365;z=0.116;w=0.88};fov=35;aspect=1;near=0.01;far=10000;frustum=[ordered]@{left=-5;right=5;top=5;bottom=-5}}
    model_3d_info=@([ordered]@{position=[ordered]@{x=0;y=0;z=0};quaternion=[ordered]@{x=0;y=0;z=0;w=1};scale=[ordered]@{x=1;y=1;z=1}})
}

$body = @{ prompt = $prompt; client_id = 'codex-astronaut-multiview' } | ConvertTo-Json -Depth 100
$body | Set-Content -LiteralPath 'D:\WorkTogetherCoOp\astronaut_prompt_debug.json' -Encoding utf8
$result = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8190/prompt' -ContentType 'application/json' -Body $body
$result | ConvertTo-Json -Compress
