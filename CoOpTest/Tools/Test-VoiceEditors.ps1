param([string]$Project='D:\WorkTogetherCoOp\CoOpTest',[string]$Client='D:\WorkTogetherCoOp\CoOpTest\Library\VP\mppmee16feb0')
$ErrorActionPreference='Stop'
$results=[System.Collections.Generic.List[string]]::new()
function Send([string]$Peer,[string]$Command,[hashtable]$Extra=@{}) { & "$Project\Tools\Invoke-WorkshopEditor.ps1" -Project $Peer -Command $Command @Extra }
function State([string]$Peer) {
    $text=Send $Peer 'voicestatus'; $values=@{}
    [regex]::Matches($text,'(sent|received|relayed|rejected|outputSamples|range)=(\d+)') | ForEach-Object { $values[$_.Groups[1].Value]=[int]$_.Groups[2].Value }
    return $values
}
function Check([bool]$Condition,[string]$Name) { if(!$Condition) { throw "FAIL: $Name" }; $results.Add("PASS: $Name"); Write-Output "PASS: $Name" }
# Requires both MPPM peers already connected in GameWorld. Synthetic signals never open a mic.
Send $Project 'menupage' @{Target='game'} | Out-Null
Send $Project 'voicerange' @{X=8} | Out-Null
Send $Project 'arrange' @{Player=0;X=0;Y=1.08;Z=-5} | Out-Null
Send $Project 'arrange' @{Player=1;X=2;Y=1.08;Z=-5} | Out-Null
$before=State $Client
Send $Project 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1600
$after=State $Client
Check ($after.received -gt $before.received) 'nearby host speech reaches client'
Check ($after.outputSamples -gt $before.outputSamples) 'decoded speech consumed by Unity audio callback'
$before=State $Project
Send $Client 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1600
$after=State $Project
Check ($after.received -gt $before.received) 'nearby client speech reaches host'
Send $Project 'arrange' @{Player=1;X=9;Y=1.08;Z=7} | Out-Null
$before=State $Client
Send $Project 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1400
$after=State $Client
Check ($after.received -eq $before.received) 'server does not relay speech outside hearing radius'
Send $Client 'voicerange' @{X=40} | Out-Null
Check ((State $Client).range -eq 8) 'client cannot change host hearing radius'
Send $Project 'voicerange' @{X=30} | Out-Null
Start-Sleep -Milliseconds 300
Check ((State $Client).range -eq 30) 'host rule change replicated'
$before=State $Client
Send $Project 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1400
Check ((State $Client).received -gt $before.received) 'larger host radius admits previously distant listener'
Send $Client 'voicedeafen' | Out-Null
$before=State $Client
Send $Project 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1400
Check ((State $Client).received -eq $before.received) 'deafen suppresses received playback'
Send $Client 'voicedeafen' @{Target='false'} | Out-Null
Send $Project 'voicemute' | Out-Null
$before=State $Project
Send $Project 'voicetone' @{Seconds=1} | Out-Null
Start-Sleep -Milliseconds 1400
Check ((State $Project).sent -eq $before.sent) 'microphone mute suppresses transmission'
Send $Project 'voicemute' @{Target='false'} | Out-Null
Send $Project 'menupage' @{Target='settings'} | Out-Null
Check ((Send $Project 'voicestatus') -match 'settings=True') 'settings panel is accessible during gameplay'
Send $Project 'voicescreen' | Out-Null
$directory=Join-Path $Project 'TestResults/Voice'; New-Item -ItemType Directory -Force $directory | Out-Null
$results | Set-Content (Join-Path $directory 'results.txt')
