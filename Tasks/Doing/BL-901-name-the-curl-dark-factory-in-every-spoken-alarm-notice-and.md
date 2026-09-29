---
id: BL-901
title: Name the Curl dark factory in every spoken alarm, notice and whisper
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1, .claude/hooks/whisper-milestone.ps1]
requirement: none
created: 2026-09-29
completed:
---
# BL-901 — Name the Curl dark factory in every spoken alarm, notice and whisper

## Goal

Everything Curl's scripts say aloud names Curl, so Stewart can tell it from the Surl dark
factory, which runs on the same PC with the same voice.

## Context

Stewart, 2026-09-29: "make sure when you use Audio that you state the Curl Dark Factory
and not just Dark Factory. I do not know what Dark Factory is talking to me." The alarm
and the out-of-tokens notices said "the dark factory", and the whisper hook's milestones
("Task done", "Committed", "Branch ..., deleted") named no project at all.

## Acceptance criteria

- [ ] `Get-AlarmSpeech` and the three spoken out-of-tokens notices say "the Curl dark
      factory", or start with "Curl", through one `$SpokenName` variable.
- [ ] Every whisper phrase from `.claude/hooks/whisper-milestone.ps1` starts with "Curl.".
- [ ] Both scripts parse, and the factory's self-checks pass.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
