---
id: BL-5189
title: Fix AF-0744: RunDarkFactory.ps1's -TestPlatform parameter help describes the audio-off self-test, and -TestAudioOff has none
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-11-14
completed:
---
# BL-5189 — Fix AF-0744: RunDarkFactory.ps1's -TestPlatform parameter help describes the audio-off self-test, and -TestAudioOff has none

## Goal

The defect the audit office reported as AF-0744 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0744 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `RunDarkFactory.ps1:574`

Location: `RunDarkFactory.ps1:574`

Param block lines 571-575: '# Prove the audio-off file silences the alarm, chimes and spoken notices, and that they / # sound as before without it, playing nothing (BL-1456), and exit. / [switch]$TestPlatform, / [switch]$TestAudioOff,'. The audio-off comment sits above $TestPlatform, so PowerShell attaches it to that parameter. Get-Help ./RunDarkFactory.ps1 -Parameter TestPlatform prints 'Prove the audio-off file silences the alarm, chimes and spoken notices...', and -TestAudioOff has an empty description. What -TestPlatform actually does is in the header (line 38: 'proves the PowerShell executable and data folder the factory picks for this OS (BL-3919)') and in DarkFactory/Platform.ps1:82.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-Help ./RunDarkFactory.ps1 -Parameter TestPlatform | Out-String) -match 'audio-off'
```

- Expected: False: -TestPlatform's help describes the platform check, and -TestAudioOff's describes the audio-off check.
- Actual: True: -TestPlatform's description is 'Prove the audio-off file silences the alarm, chimes and spoken notices, and that they sound as before without it, playing nothing (BL-1456), and exit.'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
