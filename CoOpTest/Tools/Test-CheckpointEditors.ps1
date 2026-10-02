param(
    [string]$Project = 'D:\WorkTogetherCoOp\CoOpTest',
    [Parameter(Mandatory=$true)][string]$Client
)
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[string]]::new()
$empty = [ulong]::MaxValue
function Send($peer, $command, [hashtable]$options = @{}) { & "$Project/Tools/Invoke-WorkshopEditor.ps1" -Project $peer -Command $command @options }
function State($peer) { Send $peer cpstate | ConvertFrom-Json }
function Check($condition, $name) { if (!$condition) { throw "FAIL: $name" }; $results.Add("PASS: $name"); Write-Output "PASS: $name" }
function Arrange($id, $x, $z) { Send $Project arrange @{Player=$id;X=$x;Y=1.1;Z=$z} | Out-Null }
function WaitPhase($phase, $seconds = 20) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do { $state=State $Project; if($state.phase -eq $phase) { return $state }; Start-Sleep -Milliseconds 250 } while((Get-Date) -lt $deadline)
    throw "Timed out waiting for $phase; got $($state.phase)"
}
try {
    $snapshot = Send $Client snapshot | ConvertFrom-Json
    $clientId = ($snapshot.players | Where-Object owner -ne 0 | Select-Object -First 1).owner
    Check ($snapshot.connected -and $snapshot.players.Count -eq 2) 'Two MPPM peers connected in checkpoint scene'
    $initial = WaitPhase Waiting
    Send $Project cpconsoleclear | Out-Null; Send $Client cpconsoleclear | Out-Null
    Arrange $clientId -11 -8
    Send $Client cppapers | Out-Null
    Check ((State $Project).documents.Count -eq 0) 'Server rejects document request from outside interaction range'
    Arrange 0 1 0; Arrange $clientId 1 2.8
    Send $Project interact @{Target='Traveller Shuttle'} | Out-Null
    Check ((State $Project).panel) 'Traveller interaction opens the local dialogue'
    Send $Project cppapers | Out-Null; Send $Client cppapers | Out-Null
    Start-Sleep -Milliseconds 350
    $state=State $Project; $remote=State $Client
    Check ($state.documents.Count -eq 2 -and $remote.documents.Count -eq 2) 'Competing paper requests create exactly two replicated documents'
    Send $Project cpclose | Out-Null; Send $Client cpclose | Out-Null
    Arrange 0 -.9 -.5; Arrange $clientId .6 -.5
    Send $Project cppickup @{Player=0} | Out-Null; Send $Client cppickup @{Player=0} | Out-Null
    $snapshot=Send $Project snapshot | ConvertFrom-Json
    Check (($snapshot.players | Where-Object held -ne $empty).Count -eq 1) 'Exclusive pickup: only one player can hold the same document'
    Send $Client cppickup @{Player=1} | Out-Null
    Start-Sleep -Milliseconds 200
    $snapshot=Send $Project snapshot | ConvertFrom-Json
    Check (($snapshot.players | Where-Object held -ne $empty).Count -eq 2) 'Crew can split ID and registration between two players'
    Send $Client cpinspect | Out-Null
    Check ((State $Client).inspecting -and !(State $Project).modal) 'Held-document inspection is local to the inspecting player'
    Send $Client cpscreen @{Target='ClientInspect'} | Out-Null
    Send $Client cpclose | Out-Null
    Arrange 0 -6 .4; Arrange $clientId -4 .4
    Send $Project cpdecide @{Target='approve'} | Out-Null
    Check ((State $Project).phase -eq 'Reviewing') 'Decision is rejected before all documents have been scanned'
    Send $Project interact @{Target='Scanner Tray 1'} | Out-Null
    Send $Client interact @{Target='Scanner Tray 2'} | Out-Null
    Start-Sleep -Milliseconds 350
    $state=State $Project; $remote=State $Client
    Check ($state.scanned -eq 2 -and $remote.scanned -eq 2) 'Two scanner trays update the shared case on both peers'
    Check (($state.documents | Where-Object socket -ne $empty).Count -eq 2) 'Both physical papers are placed in scanner sockets'
    Send $Project cppickup @{Player=0} | Out-Null
    Check (((State $Project).documents | Where-Object index -eq 0).holder -ne $empty) 'A scanned paper can be removed and shared again'
    Arrange 0 -9 -7; Send $Project throw | Out-Null
    Start-Sleep -Milliseconds 700
    $paper=(State $Project).documents | Where-Object index -eq 0
    Check ($paper.holder -eq $empty -and $paper.socket -eq $empty) 'Throwing releases the document into server physics'
    Start-Sleep -Milliseconds 1300
    $paper=(State $Project).documents | Where-Object index -eq 0
    Arrange $clientId ($paper.position.x - 1) ($paper.position.z - .8)
    Send $Client cppickup @{Player=0} | Out-Null
    Check (((State $Project).documents | Where-Object index -eq 0).holder -ne $empty) 'The other player can collect a thrown paper'
    Send $Client drop | Out-Null
    Arrange $clientId -12 -10; Send $Client cpdecide @{Target='approve'} | Out-Null
    Check ((State $Project).phase -eq 'Reviewing') 'Remote approval request is rejected away from the terminal'
    Arrange 0 -5 .5; Arrange $clientId -3.5 1
    Send $Project interact @{Target='Inspection Terminal'} | Out-Null
    Send $Project cpscreen @{Target='TerminalComplete'} | Out-Null
    Send $Project cpdecide @{Target='approve'} | Out-Null
    Send $Client cpdecide @{Target='reject'} | Out-Null
    $state=State $Project
    Check ($state.approved -eq 1 -and $state.rejected -eq 0 -and $state.phase -eq 'Approved') 'Competing decisions commit exactly one outcome'
    Start-Sleep -Milliseconds 700
    $a=State $Project; $b=State $Client
    Check ($a.vehicle.z -gt 1 -and [math]::Abs($a.vehicle.z-$b.vehicle.z) -lt 1) 'Approved vehicle travels forward on both peers'
    $next=WaitPhase Waiting
    Check ($next.visit -eq $initial.visit+1 -and $next.documents.Count -eq 0) 'Next traveller arrives and old documents are cleaned up'
    Arrange 0 1 0; Send $Project cppapers @{Player=$initial.visit} | Out-Null
    Check ((State $Project).documents.Count -eq 0) 'Stale UI request cannot issue papers for a new visit'
    Send $Project cppapers | Out-Null
    Arrange 0 -.9 -.5; Send $Project cppickup @{Player=0} | Out-Null
    Arrange 0 -6 .4; Send $Project interact @{Target='Scanner Tray 1'} | Out-Null
    Send $Project drop | Out-Null
    Arrange $clientId .6 -.5; Send $Client cppickup @{Player=1} | Out-Null
    Arrange $clientId -4 .4; Send $Client interact @{Target='Scanner Tray 2'} | Out-Null
    Arrange 0 -5 .5; Send $Project cpdecide @{Target='reject'} | Out-Null
    Start-Sleep -Milliseconds 900
    $state=State $Project; $remote=State $Client
    Check ($state.phase -eq 'Rejected' -and $remote.phase -eq 'Rejected' -and $state.vehicle.z -lt 1 -and $state.vehicle.y -gt 0) 'Rejection kicks the shuttle up and back toward the portal on both peers'
    Send $Project cpscreen @{Target='Rejection'} | Out-Null
    $next=WaitPhase Waiting
    Check ($next.visit -eq $initial.visit+2 -and $next.rejected -eq 1 -and $next.documents.Count -eq 0) 'Rejection completes and the next visit starts cleanly'
    Check ((Send $Project cpconsole) -eq 'No Console errors') 'Host Console has no gameplay errors'
    Check ((Send $Client cpconsole) -eq 'No Console errors') 'Client Console has no gameplay errors'
} catch { $results.Add($_.Exception.Message); throw }
finally {
    $folder=Join-Path $Project 'TestResults/Checkpoint'; New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $results | Set-Content (Join-Path $folder 'MPPM-results.txt')
}
