param(
    [string]$Project = 'D:\WorkTogetherCoOp\CoOpTest',
    [Parameter(Mandatory=$true)][string]$Client
)
# Run after two MPPM players enter GameWorld through MainMenu. Uses existing gameplay/debug entry points.
$ErrorActionPreference = 'Stop'
$results = [System.Collections.Generic.List[string]]::new()
function Send($peer, $command, [hashtable]$options = @{}) { & "$Project/Tools/Invoke-WorkshopEditor.ps1" -Project $peer -Command $command @options }
function Check($condition, $name) { if (!$condition) { throw "FAIL: $name" }; $results.Add("PASS: $name"); Write-Output "PASS: $name" }
function BothArt { @((Send $Project cpvisitorart), (Send $Client cpvisitorart)) }
function Arrange($id, $x, $z) { Send $Project arrange @{Player=$id;X=$x;Y=1.1;Z=$z} | Out-Null }
function Debug($command, $arg=0, $flaw=-1) { Send $Project cpdebug @{Target=$command;Player=$arg;X=$flaw} | Out-Null }
try {
    $deadline=(Get-Date).AddSeconds(45)
    do {
        $snapshot=Send $Project snapshot | ConvertFrom-Json
        if($snapshot.connected -and $snapshot.players.Count -eq 2) { break }
        Start-Sleep -Milliseconds 1000
    } while((Get-Date) -lt $deadline)
    $remote=($snapshot.players | Where-Object owner -ne 0 | Select-Object -First 1).owner
    Check ($snapshot.connected -and $snapshot.players.Count -eq 2) 'Two MPPM players in GameWorld'
    Send $Project move @{Seconds=120} | Out-Null; Send $Client move @{Seconds=120} | Out-Null
    Send $Project cpconsoleclear | Out-Null; Send $Client cpconsoleclear | Out-Null
    Debug visitorkind 0 0; Debug arrive
    Arrange 0 2.8 0; Arrange $remote 3.8 1
    Start-Sleep -Milliseconds 600
    $art=BothArt
    Check (($art | Where-Object {$_ -notmatch 'seated=True seatedClip=True'}).Count -eq 0) 'Private visitor seated immediately on both peers'
    Check (($art | Where-Object {$_ -notmatch 'target=player 0'}).Count -eq 0) 'Both peers select the nearer host player'
    Arrange 0 4.4 1; Arrange $remote 2.8 0
    Start-Sleep -Milliseconds 600
    $art=BothArt
    Check (($art | Where-Object {$_ -notmatch "target=player $remote"}).Count -eq 0) 'Both peers switch to the nearer remote player'
    Arrange 0 8 0; Arrange $remote 8 2
    Start-Sleep -Milliseconds 700
    $art=BothArt
    Check (($art | Where-Object {$_ -match 'target=player'}).Count -eq 0) 'No gaze target when all players are outside 5 metres'
    Arrange 0 0 3.7
    Send $Project interact @{Target='Traveller Shuttle'} | Out-Null
    Send $Project cppapers | Out-Null
    $a=Send $Project cpstate | ConvertFrom-Json; $b=Send $Client cpstate | ConvertFrom-Json
    Check ($a.documents.Count -gt 0 -and $a.documents.Count -eq $b.documents.Count -and $a.scanned -eq $a.documents.Count) 'Normal dialogue hands scanned documents into replicated slots'
    Send $Project cpclose | Out-Null; Debug approve
    Check ((Send $Project cpstate | ConvertFrom-Json).phase -eq 'Approved') 'Private visitor approval and departure'
    Debug visitorcompanions 0 0; Debug arrive
    $art=BothArt
    Check (($art | Where-Object {$_ -notmatch 'actors=2'}).Count -eq 0) 'Driver and companion both rendered on each peer'
    Debug arrest
    $cell=Send $Project cpcell; $remoteCell=Send $Client cpcell
    Check ($cell -match 'occupied=2/' -and $remoteCell -match 'occupied=2/') 'Arrest transfers both travellers to replicated holding cell'
    $art=BothArt
    Check (($art | Where-Object {$_ -match 'active=True seated=True'}).Count -eq 0) 'Impounded vehicle leaves without its occupants'
    Debug releaseall
    Debug visitorkind 1 0; Debug arrive
    $art=BothArt
    Check (($art | Where-Object {$_ -notmatch 'kind=Truck' -or $_ -notmatch 'seated=True seatedClip=True'}).Count -eq 0) 'Freight truck has seated travellers on both peers'
    $cargo=Send $Project cpcargostate | ConvertFrom-Json
    Check ($cargo.packages.Count -gt 0 -and (Send $Project cpcargoaboard) -match 'ready=True') 'Truck arrives with usable cargo loaded'
    $package=$cargo.packages | Where-Object index -eq 0
    Arrange 0 $package.position.x 2.6
    Send $Project cppickuppackage @{Player=0} | Out-Null
    Check ((Send $Project cpcargoaboard) -match 'ready=False') 'Crate can be picked up from the new bed'
    Debug approve
    Check ((Send $Project cpstate | ConvertFrom-Json).phase -eq 'Waiting') 'Approval blocked while cargo is missing'
    Send $Project cptruckspot @{Player=0} | Out-Null
    Check ((Send $Project cpcargoaboard) -match 'ready=True') 'Crate can be returned to its fitted bed slot'
    Debug approve
    Check ((Send $Project cpstate | ConvertFrom-Json).phase -eq 'Approved') 'Reloaded cargo truck departs'
    Debug visitorkind 0 0; Debug arrive; Debug reject
    Check ((Send $Project cpstate | ConvertFrom-Json).phase -eq 'Rejected') 'Rejection keeps existing kick/departure flow'
    Check ((Send $Project cpconsole) -eq 'No Console errors') 'No host gameplay errors'
    Check ((Send $Client cpconsole) -eq 'No Console errors') 'No client gameplay errors'
} catch { $results.Add($_.Exception.Message); throw }
finally {
    $folder=Join-Path $Project 'TestResults/VisitorArt'; New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $results | Set-Content (Join-Path $folder 'MPPM-results.txt')
}
