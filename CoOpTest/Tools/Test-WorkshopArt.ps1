param([string]$Project='D:\WorkTogetherCoOp\CoOpTest', [string]$ClientProject='D:\WorkTogetherCoOp\CoOpTest\Library\VP\mppmee16feb0')
$ErrorActionPreference='Stop'
$report = Join-Path $Project 'TestResults\Art\MultiplayerPlayMode.txt'
$lines = [System.Collections.Generic.List[string]]::new()
$empty = [ulong]::MaxValue
function Send($Peer, $Command, $Target='') { & "$PSScriptRoot\Invoke-WorkshopEditor.ps1" -Project $Peer -Command $Command -Target $Target }
function Snap($Peer) { Send $Peer snapshot | ConvertFrom-Json }
function PlacePlayer($Id,$X,$Z) { & "$PSScriptRoot\Invoke-WorkshopEditor.ps1" -Project $Project -Command arrange -Player $Id -X $X -Y 1.08 -Z $Z | Out-Null }
function Check($Condition,$Name) {
    if (!$Condition) { throw "FAIL: $Name" }
    $lines.Add("PASS: $Name"); Write-Output "PASS: $Name"
}
function Distance($A,$B) { [math]::Sqrt([math]::Pow($A.x-$B.x,2)+[math]::Pow($A.y-$B.y,2)+[math]::Pow($A.z-$B.z,2)) }
try {
    Send $Project host
    Send $ClientProject client
    Start-Sleep -Seconds 2
    $server = Snap $Project; $client = Snap $ClientProject
    Check ($server.players.Count -eq 2 -and $client.players.Count -eq 2 -and $client.connected) 'Host and MPPM editor client connected with two players'
    $clientId = ($client.players | Where-Object owner -ne 0).owner
    PlacePlayer $clientId 1 -2
    PlacePlayer 0 -1 -2
    Send $ClientProject pickup Crate
    Start-Sleep -Milliseconds 400
    $server = Snap $Project; $client = Snap $ClientProject
    $carrier = $client.players | Where-Object owner -eq $clientId
    $held = $carrier.held
    Check ($held -ne $empty -and $carrier.animation -eq 'Carry' -and ($server.players | Where-Object owner -eq $clientId).carrying) 'Client pickup and carrying pose replicated to host'
    Send $Project pickup Crate
    $server = Snap $Project
    Check (($server.players | Where-Object owner -eq 0).held -eq $empty) 'Contested pickup rejected'
    $before = $carrier.position
    & "$PSScriptRoot\Invoke-WorkshopEditor.ps1" -Project $ClientProject -Command move -X 1 -Seconds 1.5
    Start-Sleep -Milliseconds 350
    $server = Snap $Project; $client = Snap $ClientProject
    $moving = $server.players | Where-Object owner -eq $clientId
    Check ($moving.animation -eq 'Carry Walk' -and (Distance $before $moving.position) -gt .3) 'Carry-walk animation and server movement observed on host'
    Start-Sleep -Seconds 2
    PlacePlayer $clientId 1 -3
    Send $ClientProject throw
    $server = Snap $Project; $client = Snap $ClientProject
    $a = $server.players | Where-Object owner -eq $clientId
    $b = $client.players | Where-Object owner -eq $clientId
    Check ($a.animation -eq 'Throw' -and $b.animation -eq 'Throw' -and [math]::Abs($a.throwStarted-$b.throwStarted) -lt .01) 'Throw animation and server timestamp synchronized on both editors'
    Start-Sleep -Seconds 2
    $server = Snap $Project; $client = Snap $ClientProject
    $thrown = $server.items | Where-Object id -eq $held
    $replica = $client.items | Where-Object id -eq $held
    Check ($thrown.holder -eq $empty -and $thrown.position.z -gt -1 -and (Distance $thrown.position $replica.position) -lt .3) 'Thrown object released and authoritative physics replicated'
    Check (($client.players | Where-Object owner -eq $clientId).animation -eq 'Idle') 'Throw returns to idle'
    PlacePlayer $clientId -2 -1
    Send $ClientProject pickup Tool
    Send $ClientProject drop
    $server=Snap $Project
    Check (($server.items | Where-Object name -eq 'Tool').holder -eq $empty) 'Ordinary drop preserved'
    Send $ClientProject pickup Tool
    PlacePlayer $clientId -5.2 1.3
    Send $ClientProject interact 'Shelf Slot 1'
    $server=Snap $Project; $client=Snap $ClientProject
    Check (($server.items | Where-Object name -eq 'Tool').socket -ne $empty -and ($client.items | Where-Object name -eq 'Tool').socket -ne $empty) 'Modeled tool placed in generic shelf slot on both peers'
    Send $ClientProject interact 'Shelf Slot 1'
    $client=Snap $ClientProject
    Check (($client.players | Where-Object owner -eq $clientId).held -ne $empty) 'Slot removal returns to carrying'
    Send $ClientProject drop
    PlacePlayer $clientId 2 -1
    Send $ClientProject pickup Battery
    PlacePlayer $clientId 4 1.25
    Send $ClientProject interact 'Generator Battery Socket'
    $server=Snap $Project; $client=Snap $ClientProject
    Check (($server.states | Where-Object name -eq 'Generator Battery Socket').value -and ($client.states | Where-Object name -eq 'Generator Battery Socket').value) 'Modeled battery powers generator on both peers'
    PlacePlayer 0 -5.2 .7
    Send $Project pickup Tool
    $server=Snap $Project
    Check (($server.players | Where-Object owner -eq 0).carrying) 'Host holds an item before late join'
    Send $ClientProject disconnect
    Start-Sleep -Seconds 1
    Send $ClientProject client
    Start-Sleep -Seconds 3
    $client=Snap $ClientProject
    Check ($client.connected -and ($client.states | Where-Object name -eq 'Generator Battery Socket').value) 'Late join reconstructs generator power'
    Check (($client.players | Where-Object owner -eq 0).carrying) 'Late join reconstructs held item and carrying pose'
    Send $Project drop
    Send $Project capture
    $lines.Add('No standalone build used. Editors left connected. Source: actual RPCs and replicated state in Multiplayer Play Mode.')
} finally {
    New-Item -ItemType Directory -Force (Split-Path $report) | Out-Null
    $lines | Set-Content -LiteralPath $report
}
