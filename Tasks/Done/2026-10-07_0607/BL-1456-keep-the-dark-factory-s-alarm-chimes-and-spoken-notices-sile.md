---
id: BL-1456
title: Keep the dark factory's alarm, chimes and spoken notices silent while the audio-off file exists
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, CLAUDE.md]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1456 — Keep the dark factory's alarm, chimes and spoken notices silent while the audio-off file exists

## Goal

While `%LOCALAPPDATA%\Curl\audio-off` exists, a running shift plays no chime, siren or speech and never touches the volume, exactly as `-QuietAlarm` does, checked at the moment it would sound, so turning audio off takes effect on a shift that is already running.

## Context

- Stewart turns Curl's audio off and on as it suits him ("turn audio off" 2026-10-03, "audio on" and "Audio off" 2026-10-04). BL-1317 made every whisper obey the file (`whisper-milestone.ps1`), but `RunDarkFactory.ps1`'s alarm (`Invoke-Alarm`: chime, siren, speech, raising the master volume and unmuting at stage 3) and its out-of-tokens and shift notices obey only `-QuietAlarm`, a start-up switch. A running shift cannot be given it: `-Restart` keeps the old arguments, and CLAUDE.md forbids hand-killing a shift to restart it.
- The file is read at each sound, not once at start, so it can be created or deleted while a shift runs.

## Acceptance criteria

- [x] Every place `RunDarkFactory.ps1` plays a sound, speaks or changes the volume or mute first checks the file (one function, e.g. `Test-AudioOff`) and behaves as under `-QuietAlarm` while it exists: the banner and notices still show on screen.
- [x] The script's self-test (or a new case in it) proves: with the file present, the alarm path makes no sound and no volume change; with it absent, behaviour is unchanged. No sound is played during the test.
- [x] The script header and root CLAUDE.md say the file silences the factory's alarm and notices too, and that `-QuietAlarm` is then not needed.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- One check, `Test-AudioOff` (the file under `%LOCALAPPDATA%\Curl\audio-off`, path held in `$script:AudioOffFile` so the self-test can point it at a temporary file), read at each sound; `Test-AlarmSilent` combines it with `-QuietAlarm` and replaces the three `if ($QuietAlarm)` guards in `Invoke-AlarmSound`, `Show-LimitNotice` and `Show-AuditNotice` - the only places the script chimes, sirens, speaks or calls `Set-AlarmVolume`. `Restore-AlarmVolume` only undoes a change that was made, so it needs no guard.
- `-TestAudioOff` replaces the chime, siren, volume and voice with recorders and proves: without the file all three paths sound as before; with it nothing sounds; deleting it mid-run brings the sound back. 3 PASS, nothing played.
- The guard switch in the self-test avoids the capitalised word the audit-path guard matches; the notice text there is a rehearsal line.
## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. The audio-off file now silences the dark factory's alarm, chimes and spoken notices on a running shift; -TestAudioOff proves it
