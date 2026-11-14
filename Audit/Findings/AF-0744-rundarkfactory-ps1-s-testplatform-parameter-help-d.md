---
id: AF-0744
title: RunDarkFactory.ps1's -TestPlatform parameter help describes the audio-off self-test, and -TestAudioOff has none
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:RunDarkFactory.ps1:TestPlatform:false-help
reproduction: none
task: none
tasks:
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0744 - RunDarkFactory.ps1's -TestPlatform parameter help describes the audio-off self-test, and -TestAudioOff has none

## Summary

Medium finding from the truthfulness auditor at `RunDarkFactory.ps1:574`: RunDarkFactory.ps1's -TestPlatform parameter help describes the audio-off self-test, and -TestAudioOff has none.

## Evidence

Location: `RunDarkFactory.ps1:574`

Param block lines 571-575: '# Prove the audio-off file silences the alarm, chimes and spoken notices, and that they / # sound as before without it, playing nothing (BL-1456), and exit. / [switch]$TestPlatform, / [switch]$TestAudioOff,'. The audio-off comment sits above $TestPlatform, so PowerShell attaches it to that parameter. Get-Help ./RunDarkFactory.ps1 -Parameter TestPlatform prints 'Prove the audio-off file silences the alarm, chimes and spoken notices...', and -TestAudioOff has an empty description. What -TestPlatform actually does is in the header (line 38: 'proves the PowerShell executable and data folder the factory picks for this OS (BL-3919)') and in DarkFactory/Platform.ps1:82.

## Reproduction

Run from the repository root:

```powershell
(Get-Help ./RunDarkFactory.ps1 -Parameter TestPlatform | Out-String) -match 'audio-off'
```

- Expected: False: -TestPlatform's help describes the platform check, and -TestAudioOff's describes the audio-off check.
- Actual: True: -TestPlatform's description is 'Prove the audio-off file silences the alarm, chimes and spoken notices, and that they sound as before without it, playing nothing (BL-1456), and exit.'

## Re-audits

## Log

- 2026-11-13: filed proposed.
