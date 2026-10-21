---
id: BL-2989
title: Start a shift beside an audit run -AlongsideShift
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/AuditCadence.ps1]
lane: no
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2989 — Start a shift beside an audit run -AlongsideShift

## Goal

A shift that starts while an audit started with `-AlongsideShift` runs (a `-Restart`, or a
`-Continuous` hand-over) starts at once instead of waiting for the audit to end.

## Context

On 2026-10-21 at 14:57 Arizona a `-Restart` for BL-2988 started a shift that waited ("An audit is
running; shifts start between audits") behind `RunAudit.ps1 -AlongsideShift`, which had been started
at 14:10 to share the machine with the shift; the factory would have sat idle until about 15:40.
`Get-ShiftStartDecision` counted every running audit except a dry run or self-test.

## Acceptance criteria

- [x] `Get-ShiftStartDecision` ignores an audit whose command line carries `-AlongsideShift`; a
      plain audit still makes a shift wait or refuse as before.
- [x] `-TestAuditCadence` proves both, and passes (10 PASS, 0 FAIL).

## Notes

## Log

- 2026-10-21: Created, worked and moved to Done in one interactive session.
