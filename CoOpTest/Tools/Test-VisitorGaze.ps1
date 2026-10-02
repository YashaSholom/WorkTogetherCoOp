param(
    [string]$Project = 'D:\WorkTogetherCoOp\CoOpTest',
    [Parameter(Mandatory=$true)][string]$Client,
    [string]$Report = 'Gaze-results.txt'
)
# Run after two MPPM players enter GameWorld. Checks the actual bone pose over time, not only target selection.
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[string]]::new()
function Send($peer, $command, [hashtable]$options = @{}) { & "$Project/Tools/Invoke-WorkshopEditor.ps1" -Project $peer -Command $command @options }
function Check($condition, $name) { if (!$condition) { throw "FAIL: $name" }; $results.Add("PASS: $name"); Write-Output "PASS: $name" }
function Arrange($id, $x, $z) { Send $Project arrange @{Player=$id;X=$x;Y=1.1;Z=$z} | Out-Null }
function Angle($a, $b) {
    $dot = [Math]::Abs($a.x*$b.x + $a.y*$b.y + $a.z*$b.z + $a.w*$b.w)
    2 * [Math]::Acos([Math]::Min([double]1, [double]$dot)) * 180 / [Math]::PI
}
function Stable($label, $target, $neutral=$false) {
    Start-Sleep -Milliseconds 2200
    $starts = @{}; [double]$maximum = 0; [double]$neutralMaximum = 0; $correct = $true; $count = 0
    for ($i=0; $i -lt 12; $i++) {
        foreach ($peer in @($Project, $Client)) {
            $poses = Send $peer cpvisitorpose | ConvertFrom-Json
            foreach ($actor in $poses.actors) {
                $count++; $key = "$peer/$($actor.id)"
                if (!$starts.ContainsKey($key)) { $starts[$key] = $actor.rotation }
                $maximum = [Math]::Max($maximum, (Angle $starts[$key] $actor.rotation))
                $neutralMaximum = [Math]::Max($neutralMaximum, (Angle $actor.rotation @{x=0;y=0;z=0;w=1}))
                if ($actor.target -ne "$target") { $correct = $false }
            }
        }
        Start-Sleep -Milliseconds 200
    }
    $results.Add("MEASURE: $label drift=$([Math]::Round($maximum,3))deg angleFromRest=$([Math]::Round($neutralMaximum,3))deg samples=$count")
    Check ($count -ge 24 -and $correct) "$label target remains correct on both peers"
    # Breathing and the vehicle's hover bob move the head relative to the player's eyes.
    Check ($maximum -lt 5) "$label stays within 5 degrees of idle motion across 12 samples per peer"
    Check ($neutralMaximum -lt $(if ($neutral) {1} else {90})) "$label head stays within rotation limits"
    if (!$neutral) { Check ($neutralMaximum -gt 5) "$label actually turns the head toward the player" }
}
try {
    $snapshot = Send $Project snapshot | ConvertFrom-Json
    $remote = ($snapshot.players | Where-Object owner -ne 0 | Select-Object -First 1).owner
    Check ($snapshot.connected -and $snapshot.players.Count -eq 2) 'Two MPPM players in GameWorld'
    foreach ($peer in @($Project, $Client)) {
        $baseline = Send $peer cpconsole
        $results.Add("BASELINE: $peer Console: $baseline")
        Send $peer cpconsoleclear | Out-Null
    }
    Send $Project move @{Seconds=240} | Out-Null; Send $Client move @{Seconds=240} | Out-Null
    Send $Project cpdebug @{Target='visitorkind';Player=0;X=0} | Out-Null
    Send $Project cpdebug @{Target='arrive'} | Out-Null
    Arrange 0 2.8 2; Arrange $remote 8 2
    Stable 'Stationary host to the side' 0
    Arrange 0 8 0; Arrange $remote 2.8 -2
    Stable 'Switch to remote player on opposite side' $remote
    Arrange 0 8 0; Arrange $remote 8 2
    Stable 'Players outside range return head to rest' 'none' $true
    Send $Project cpdebug @{Target='visitorkind';Player=1;X=0} | Out-Null
    Send $Project cpdebug @{Target='arrive'} | Out-Null
    Arrange 0 4.4 2; Arrange $remote 8 2
    Stable 'Freight truck seated idle' 0
    Send $Project cpdebug @{Target='arrest'} | Out-Null
    $standing = (Send $Project cpvisitorpose | ConvertFrom-Json).actors | Where-Object { !$_.seated } | Select-Object -First 1
    Check ($null -ne $standing) 'Visitor transfers to standing idle in holding cell'
    Arrange 0 ($standing.position.x+2) $standing.position.z
    Arrange $remote 0 0
    Stable 'Standing idle in holding cell' 0
    Check ((Send $Project cpconsole) -eq 'No Console errors') 'No host Console errors'
    Check ((Send $Client cpconsole) -eq 'No Console errors') 'No client Console errors'
} catch { $results.Add($_.Exception.Message); throw }
finally {
    $folder = Join-Path $Project 'TestResults/VisitorArt'; New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $results | Set-Content (Join-Path $folder $Report)
}
