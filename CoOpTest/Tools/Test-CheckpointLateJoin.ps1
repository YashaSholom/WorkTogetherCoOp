param([string]$Project='D:\WorkTogetherCoOp\CoOpTest', [Parameter(Mandatory=$true)][string]$Client)
$ErrorActionPreference='Stop'
$results=[System.Collections.Generic.List[string]]::new()
$empty=[ulong]::MaxValue
function Send($peer,$command,[hashtable]$options=@{}) { & "$Project/Tools/Invoke-WorkshopEditor.ps1" -Project $peer -Command $command @options }
function State($peer) { Send $peer cpstate | ConvertFrom-Json }
function Check($condition,$name) { if(!$condition) { throw "FAIL: $name" }; $results.Add("PASS: $name"); Write-Output "PASS: $name" }
function Arrange($id,$x,$z) { Send $Project arrange @{Player=$id;X=$x;Y=1.1;Z=$z} | Out-Null }
try {
    Send $Project host | Out-Null
    $deadline=(Get-Date).AddSeconds(25)
    do { Start-Sleep -Milliseconds 300; $state=State $Project } while($state.phase -ne 'Waiting' -and (Get-Date) -lt $deadline)
    Check ($state.phase -eq 'Waiting') 'Direct test scene starts a checkpoint visit without the party admission gate'
    Send $Project cpconsoleclear | Out-Null; Send $Client cpconsoleclear | Out-Null
    Arrange 0 1 0; Send $Project cppapers | Out-Null
    Arrange 0 -.9 -.5; Send $Project cppickup @{Player=0} | Out-Null
    Arrange 0 -6 .4; Send $Project interact @{Target='Scanner Tray 1'} | Out-Null
    Arrange 0 .6 -.5; Send $Project cppickup @{Player=1} | Out-Null
    Send $Client client | Out-Null
    $deadline=(Get-Date).AddSeconds(25)
    do { Start-Sleep -Milliseconds 300; $snapshot=Send $Client snapshot | ConvertFrom-Json } while(!$snapshot.connected -and (Get-Date) -lt $deadline)
    $remote=State $Client; $hostState=State $Project
    Check ($snapshot.connected -and $snapshot.players.Count -eq 2) 'Late MPPM peer joins the active checkpoint'
    Check ($remote.phase -eq 'Reviewing' -and $remote.visit -eq $hostState.visit -and $remote.caseIndex -eq $hostState.caseIndex) 'Late join reconstructs the current traveller and phase'
    Check ($remote.scanned -eq 1 -and $remote.documents.Count -eq 2) 'Late join receives previously issued documents and scanned state'
    Check (($remote.documents | Where-Object index -eq 0).socket -ne $empty -and ($remote.documents | Where-Object index -eq 1).holder -ne $empty) 'Late join reconstructs one slotted and one carried document'
    $clientId=($snapshot.players | Where-Object owner -ne 0 | Select-Object -First 1).owner
    Arrange $clientId -6 .4; Send $Client cppickup @{Player=0} | Out-Null
    Check (((State $Project).documents | Where-Object index -eq 0).holder -ne $empty) 'Late peer can retrieve an existing scanned paper'
    Send $Client disconnect | Out-Null
    Start-Sleep -Milliseconds 600
    Check (((State $Project).documents | Where-Object index -eq 0).holder -eq $empty) 'Disconnect releases a held document on the server'
    Arrange 0 -6 .4; Send $Project interact @{Target='Scanner Tray 1'} | Out-Null
    Arrange 0 -5 .5; Send $Project interact @{Target='Inspection Terminal'} | Out-Null
    Send $Project cpclose | Out-Null; Send $Project cpinspect | Out-Null
    Send $Project cpdecide @{Target='approve'} | Out-Null
    Send $Client client | Out-Null
    Start-Sleep -Milliseconds 700
    $remote=State $Client; $hostState=State $Project
    Check ($remote.approved -eq 1 -and $remote.phase -eq $hostState.phase -and [math]::Abs($remote.vehicle.z-$hostState.vehicle.z) -lt 2) 'Rejoining during departure reconstructs the decision and vehicle motion'
    $deadline=(Get-Date).AddSeconds(20)
    do { Start-Sleep -Milliseconds 250; $state=State $Project } while($state.visit -eq 1 -and (Get-Date) -lt $deadline)
    Check ($state.documents.Count -eq 0 -and !$state.inspecting -and !$state.modal) 'Departure removes held papers and closes inspection safely'
    Check ((Send $Project cpconsole) -eq 'No Console errors') 'Late-join host Console has no errors'
    Check ((Send $Client cpconsole) -eq 'No Console errors') 'Late-join client Console has no errors'
} catch { $results.Add($_.Exception.Message); throw }
finally {
    $folder=Join-Path $Project 'TestResults/Checkpoint'; New-Item -ItemType Directory -Force $folder | Out-Null
    $results | Set-Content (Join-Path $folder 'LateJoin-results.txt')
}
