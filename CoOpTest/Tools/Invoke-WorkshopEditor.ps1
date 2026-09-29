param(
    [string]$Project = 'D:\WorkTogetherCoOp\CoOpTest',
    [Parameter(Mandatory = $true)][string]$Command,
    [string]$Target = '',
    [ulong]$Player = 0,
    [float]$X = 0, [float]$Y = 0, [float]$Z = 0,
    [float]$Heading = 0,
    [float]$Seconds = 1,
    [int]$TimeoutSeconds = 40
)
# Sends one explicit request to an already-running editor; never starts a standalone build.
$temp = Join-Path $Project 'Temp'
$requestPath = Join-Path $temp 'WorkshopRequest.json'
$responsePath = Join-Path $temp 'WorkshopResponse.txt'
if (Test-Path -LiteralPath $requestPath) { throw 'An editor request is already pending.' }
if (Test-Path -LiteralPath $responsePath) { Remove-Item -LiteralPath $responsePath }
@{ command=$Command; target=$Target; player=$Player; position=@{x=$X;y=$Y;z=$Z}; heading=$Heading; seconds=$Seconds } |
    ConvertTo-Json | Set-Content -LiteralPath ($requestPath + '.pending')
Move-Item -LiteralPath ($requestPath + '.pending') -Destination $requestPath
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while (!(Test-Path -LiteralPath $responsePath)) {
    if ((Get-Date) -gt $deadline) { throw "Editor did not respond: $Project" }
    Start-Sleep -Milliseconds 200
}
$response = Get-Content -LiteralPath $responsePath -Raw
if ($response.StartsWith('ERROR:')) { throw $response }
$response
