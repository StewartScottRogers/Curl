# -TestAudioOff: the audio-off file silences the alarm and every notice. Dot-sourced by
# RunDarkFactory.ps1 in its order; see the loader there.

if ($TestAudioOff) {
    # Proves the audio-off file silences the alarm and every notice (BL-1456) without a sound:
    # the chime, siren, volume and voice are replaced by recorders, and the file is a
    # throwaway one in a temporary folder, never Stewart's real switch.
    $script:audioOffFailed = 0
    $check = { param([string]$Name, [bool]$Ok, [string]$Detail) if (-not $Ok) { $script:audioOffFailed++ }; Write-Host "$(if ($Ok) { 'PASS' } else { 'FAIL' }) ${Name}: $Detail" -ForegroundColor $(if ($Ok) { 'Green' } else { 'Red' }) }
    $script:Sounds = [System.Collections.Generic.List[string]]::new()
    function Invoke-Chime { $script:Sounds.Add('chime') }
    function Invoke-Siren { param([int]$Sweeps) $script:Sounds.Add("siren $Sweeps") }
    function Set-AlarmVolume { $script:Sounds.Add('volume') }
    function Get-Voice {
        $v = [pscustomobject]@{ Volume = 0; Rate = 0 }
        $v | Add-Member ScriptMethod SpeakAsyncCancelAll { }
        $v | Add-Member ScriptMethod SpeakAsync { param($Text) $script:Sounds.Add('speech') }
        return $v
    }
    $QuietAlarm = $false
    $dir = Join-Path ([IO.Path]::GetTempPath()) "curl-audio-off-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    New-Item -ItemType Directory -Path $dir | Out-Null
    $script:AudioOffFile = Join-Path $dir 'audio-off'
    $makeSounds = {
        $script:Sounds.Clear()
        Invoke-AlarmSound -Reasons @('BL-004 DECIDE   Decide the licence') -Stage 3
        Show-LimitNotice 'OUT OF TOKENS' 'Rehearsal.' 'Rehearsal.' 'Yellow'
        Show-AuditNotice 'Rehearsal of the due notice'
        return ($script:Sounds -join ', ')
    }
    $loud = 'volume, siren 3, speech, chime, speech, chime, speech'
    try {
        $heard = & $makeSounds
        & $check 'without the file the alarm and notices sound as before' ($heard -eq $loud) $heard
        Set-Content -LiteralPath $script:AudioOffFile -Value ''
        $heard = & $makeSounds
        & $check 'with the file nothing plays, speaks or changes the volume' (-not $heard) "'$heard'"
        Remove-Item -LiteralPath $script:AudioOffFile
        $heard = & $makeSounds
        & $check 'deleting the file while running brings the sound back' ($heard -eq $loud) $heard
    } finally { Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue }
    exit $(if ($script:audioOffFailed) { 1 } else { 0 })
}

# This file ran to its end; RunDarkFactory.ps1 goes on to the next one.
$AreaFileFinished = $true
