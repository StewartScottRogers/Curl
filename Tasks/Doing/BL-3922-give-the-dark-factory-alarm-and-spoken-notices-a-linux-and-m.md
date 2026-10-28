---
id: BL-3922
title: Give the dark factory alarm and spoken notices a Linux and macOS path
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3919]
touches: [DarkFactory/Alarm.ps1, DarkFactory/AudioOff.ps1]
requirement: none
created: 2026-10-27
completed:
---
# BL-3922 — Give the dark factory alarm and spoken notices a Linux and macOS path

## Goal

On Linux and macOS the dark factory's alarm and spoken notices show their banner and text and
speak through `say` (macOS) or `spd-say`/`espeak` (Linux) when present, never touch the system
volume, and respect the audio-off file in the platform data folder; on Windows nothing changes.

## Context

Fourth of five tasks making the dark factory run under `pwsh` off Windows (after BL-3919).
`DarkFactory/Alarm.ps1` uses Windows-only pieces: an `Add-Type` Core Audio volume interop
(~line 52) and `System.Speech` (~line 101), and reads the audio-off file from `%LOCALAPPDATA%`
(line 168). `DarkFactory/AudioOff.ps1` holds `-TestAudioOff`. Off Windows, `Add-Type` of the
COM interop and `System.Speech` fail; today the alarm would throw or go silent unpredictably.
Use `Get-FactoryDataDir` from BL-3919 for the audio-off path. Audio is currently off for
Stewart (the audio-off file exists), so test with `-QuietAlarm` or a scratch data folder.

## Acceptance criteria

- [ ] Off Windows `DarkFactory/Alarm.ps1` never calls the Core Audio interop or `System.Speech`; it speaks with `say`, `spd-say` or `espeak`, the first found on PATH, and otherwise prints only.
- [ ] The audio-off file is read from `Join-Path (Get-FactoryDataDir) 'audio-off'` on every platform, which on Windows is still `%LOCALAPPDATA%\Curl\audio-off`.
- [ ] `-TestAudioOff` and `-TestAlarm -AlarmScale 0.1 -QuietAlarm` pass on Windows, and the Windows code path is unchanged.
- [ ] `dotnet build` and the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 76423c17 fa8686c1 c0a14fd3 d265299d.
