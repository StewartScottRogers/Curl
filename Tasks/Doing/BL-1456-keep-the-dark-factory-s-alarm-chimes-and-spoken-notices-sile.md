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
completed:
---
# BL-1456 — Keep the dark factory's alarm, chimes and spoken notices silent while the audio-off file exists

## Goal

While `%LOCALAPPDATA%\Curl\audio-off` exists, a running shift plays no chime, siren or speech and never touches the volume, exactly as `-QuietAlarm` does, checked at the moment it would sound, so turning audio off takes effect on a shift that is already running.

## Context

- Stewart turns Curl's audio off and on as it suits him ("turn audio off" 2026-10-03, "audio on" and "Audio off" 2026-10-04). BL-1317 made every whisper obey the file (`whisper-milestone.ps1`), but `RunDarkFactory.ps1`'s alarm (`Invoke-Alarm`: chime, siren, speech, raising the master volume and unmuting at stage 3) and its out-of-tokens and shift notices obey only `-QuietAlarm`, a start-up switch. A running shift cannot be given it: `-Restart` keeps the old arguments, and CLAUDE.md forbids hand-killing a shift to restart it.
- The file is read at each sound, not once at start, so it can be created or deleted while a shift runs.

## Acceptance criteria

- [ ] Every place `RunDarkFactory.ps1` plays a sound, speaks or changes the volume or mute first checks the file (one function, e.g. `Test-AudioOff`) and behaves as under `-QuietAlarm` while it exists: the banner and notices still show on screen.
- [ ] The script's self-test (or a new case in it) proves: with the file present, the alarm path makes no sound and no volume change; with it absent, behaviour is unchanged. No sound is played during the test.
- [ ] The script header and root CLAUDE.md say the file silences the factory's alarm and notices too, and that `-QuietAlarm` is then not needed.
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes
## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
