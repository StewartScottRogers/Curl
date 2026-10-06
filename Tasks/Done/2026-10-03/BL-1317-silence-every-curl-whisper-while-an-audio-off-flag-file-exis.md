---
id: BL-1317
title: Silence every Curl whisper while an audio-off flag file exists
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/hooks/whisper-milestone.ps1, CLAUDE.md]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1317 — Silence every Curl whisper while an audio-off flag file exists

## Goal

No Curl whisper is spoken while `%LOCALAPPDATA%\Curl\audio-off` exists, in any lane or session.

## Context

- Stewart, 2026-10-03: "turn audio off". The end-of-shift alarm had reached stage 3 (master volume raised to 100%, unmuted) and was stopped by killing its process, so the volume was not restored.
- Every whisper goes through `.claude/hooks/whisper-milestone.ps1`: the PostToolUse phrases, the Backlog depth that `task-board.ps1` starts (BL-1182), and the coordinator's "CI failure filed". One check at its top silences all of them. The coordinator's own alarm and notices are silenced by `-QuietAlarm`, which Claude passes while the file exists.

## Acceptance criteria

- [x] With the file present, `whisper-milestone.ps1 -Phrase 'Backlog depth, 1.' -PauseSeconds 2` returns at once and speaks nothing (333 ms measured; speaking takes over 2 s).
- [x] Root `CLAUDE.md` names the switch and the `-QuietAlarm` rule.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- 2026-10-03: The file was created on Stewart's machine. The 06:12 shift was started with `-QuietAlarm`.
## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Whispers are silent while the audio-off file exists
