param([string]$Build = "$PSScriptRoot/../Builds/CoopWorkshop/CoopWorkshop.exe")
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath("$PSScriptRoot/../TestResults")
[IO.Directory]::CreateDirectory($root) | Out-Null
$run = Join-Path $root (Get-Date -Format 'yyyyMMdd-HHmmss')
[IO.Directory]::CreateDirectory($run) | Out-Null
$processes = [Collections.Generic.List[Diagnostics.Process]]::new()
$results = [Collections.Generic.List[string]]::new()
function Start-Peer([string]$name, [string]$mode) {
    $probe = Join-Path $run $name
    $p = Start-Process -FilePath $Build -ArgumentList "-$mode -port 17777 -batchmode -nographics -probe $probe -logFile $probe.log" -WindowStyle Hidden -PassThru
    $processes.Add($p)
}
function State([string]$peer) {
    $path = Join-Path $run "$peer.state"
    if (Test-Path -LiteralPath $path) { return Get-Content -LiteralPath $path }
    return @()
}
function Wait-For([string]$label, [scriptblock]$condition) {
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        if (& $condition) { $results.Add("PASS $label"); Write-Output "PASS $label"; return }
        Start-Sleep -Milliseconds 250
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "FAIL $label (see $run)"
}
function Send([string]$peer, [string]$command) {
    $path = Join-Path $run "$peer.command"
    $deadline = [DateTime]::UtcNow.AddSeconds(10)
    while (Test-Path -LiteralPath $path) {
        if ([DateTime]::UtcNow -gt $deadline) { throw "Command not consumed by $peer" }
        Start-Sleep -Milliseconds 100
    }
    [IO.File]::WriteAllText("$path.tmp", $command)
    Move-Item -LiteralPath "$path.tmp" -Destination $path
    Start-Sleep -Milliseconds 500
}
function Item-Line([string]$peer, [string]$name) { (State $peer) | Where-Object { $_ -match "\|$([regex]::Escape($name))\|" } | Select-Object -First 1 }
try {
    Start-Peer host host
    Wait-For 'Host starts and spawns' { (State host) -match 'NetworkPlayer' }
    Start-Peer client client
    Wait-For 'Two peers see both players' { @((State host) -match 'NetworkPlayer').Count -eq 2 -and @((State client) -match 'NetworkPlayer').Count -eq 2 }
    $hostId = (((State host) | Where-Object { $_ -match 'NetworkPlayer.*owner=0\|' }) -split '\|')[0]
    $clientId = (((State host) | Where-Object { $_ -match 'NetworkPlayer.*owner=1\|' }) -split '\|')[0]
    Send client 'interact|Door Button'
    Wait-For 'Distant interaction rejected' { (Item-Line host 'Door Button') -match 'state=False' }
    Send host 'arrange|1|0|-5'
    Send client 'move|0|1|90|0.7'
    Wait-For 'Client input moves authoritative player and replicates rotation/position' {
        $line = (State host) | Where-Object { $_ -match 'NetworkPlayer.*owner=1\|' }
        $remote = (State client) | Where-Object { $_ -match 'NetworkPlayer.*owner=1\|' }
        $line -match 'pos=\(([1-9]\d*\.\d+),' -and $remote -match 'pos=\(([1-9]\d*\.\d+),'
    }
    Send host 'arrange|0|-2|-1.5'
    Send host 'arrange|1|-3|-1.5'
    Send client 'interact|Tool'
    Wait-For 'Client pickup observed by host' { (Item-Line host Tool) -match "holder=$clientId\|" -and (Item-Line client Tool) -match "holder=$clientId\|" }
    Send host 'interact|Tool'
    Wait-For 'Competing pickup rejected' { (Item-Line host Tool) -match "holder=$clientId\|" }
    Send client drop
    Wait-For 'Drop releases synchronized item' { (Item-Line host Tool) -match 'holder=18446744073709551615' -and (Item-Line client Tool) -match 'holder=18446744073709551615' }
    Send host 'interact|Tool'
    Wait-For 'Host can pick up dropped item' { (Item-Line client Tool) -match "holder=$hostId\|" }
    Send host 'arrange|0|4|1'
    Send host 'interact|Generator Battery Socket'
    Wait-For 'Wrong category rejected' { (Item-Line host Tool) -match "holder=$hostId\|" -and (Item-Line host 'Generator Battery Socket') -match 'state=False' }
    Send host 'arrange|0|-4|1'
    Send host 'interact|Shelf Slot 2'
    Wait-For 'Generic shelf placement replicated' { (Item-Line host 'Shelf Slot 2') -notmatch 'occupant=18446744073709551615' -and (Item-Line client 'Shelf Slot 2') -notmatch 'occupant=18446744073709551615' }
    Send host 'arrange|0|0|-3'
    Send host 'arrange|1|-4|1'
    Send client 'interact|Shelf Slot 2'
    Wait-For 'Unlocked slot removal works' { (Item-Line host Tool) -match "holder=$clientId\|" }
    Send client 'interact|Shelf Slot 3'
    Wait-For 'Same item can use a second generic slot' { (Item-Line host 'Shelf Slot 3') -notmatch 'occupant=18446744073709551615' }
    Send host 'arrange|1|2|-1.5'
    Send client 'interact|Battery'
    Wait-For 'Client carries battery' { (Item-Line host Battery) -match "holder=$clientId\|" }
    Send host 'arrange|1|4|1'
    Send client 'interact|Generator Battery Socket'
    Wait-For 'Battery powers generator and lamps on both peers' { (Item-Line host 'Generator Battery Socket') -match 'state=True' -and (State host) -contains 'lamps=3' -and (State client) -contains 'lamps=3' }
    Send client 'interact|Generator Battery Socket'
    Wait-For 'Locked battery cannot be removed' { (Item-Line host Battery) -match 'holder=18446744073709551615' -and (Item-Line host 'Generator Battery Socket') -match 'state=True' }
    Send host 'arrange|0|-2|4.5'
    Send host 'interact|Door Button'
    Wait-For 'Generic button action opens synchronized door' { (State client) -contains 'door=(0.00, 4.25, 7.00)' }
    Start-Peer late client
    Wait-For 'Late join receives existing players, socket occupancy, generator lights and door state' {
        @((State late) -match 'NetworkPlayer').Count -eq 3 -and (Item-Line late 'Generator Battery Socket') -match 'state=True' -and
        (State late) -contains 'lamps=3' -and (State late) -contains 'door=(0.00, 4.25, 7.00)' -and
        (Item-Line late 'Shelf Slot 3') -notmatch 'occupant=18446744073709551615'
    }
    Send host 'arrange|1|-1|-2.5'
    Send client 'interact|Crate'
    Wait-For 'Client holds crate before disconnect' { (State host) -match "\|Crate\|.*holder=$clientId\|" }
    Send client quit
    Wait-For 'Disconnect releases carried prop and despawns player' { @((State host) -match 'NetworkPlayer').Count -eq 2 -and -not ((State host) -match "holder=$clientId\|") }
    foreach ($peer in @('host','late')) {
        $errors = Select-String -Path (Join-Path $run "$peer.log") -Pattern 'Exception|\[Error\]|\[Probe\]' -ErrorAction SilentlyContinue
        if ($errors) { throw "Runtime errors in $peer log: $errors" }
    }
    $results.Add('PASS No runtime exceptions in host/late-client logs')
}
catch { $results.Add($_.Exception.Message); throw }
finally {
    $results | Set-Content -LiteralPath (Join-Path $run 'results.txt')
    foreach ($p in $processes) { if (!$p.HasExited) { Stop-Process -Id $p.Id -ErrorAction SilentlyContinue } }
    Write-Output "Results: $run"
}

