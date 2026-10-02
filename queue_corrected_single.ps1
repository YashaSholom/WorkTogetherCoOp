$history = Invoke-RestMethod 'http://127.0.0.1:8190/history'
$prompt = $history.'e04c2262-6e92-45d1-b03a-2754fbd19284'.prompt[2]

# Quality-preserving baseline: 4096 texture bake, direct base-colour connection.
$prompt.'288'.inputs.value = 4096
$prompt.PSObject.Properties.Remove('324')
$prompt.'210'.inputs.base_color = @('164', 0)

$body = @{ prompt = $prompt; client_id = 'codex-quality-repair-single' } | ConvertTo-Json -Depth 100
$result = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:8190/prompt' -ContentType 'application/json' -Body $body
$result | ConvertTo-Json -Compress
