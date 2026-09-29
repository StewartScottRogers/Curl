---
id: BL-772
title: Record BL-519's -Z implementation choices in ADR-0127
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-519]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-772 — Record BL-519's -Z implementation choices in ADR-0127

## Goal

ADR-0127 gains an "Implementation (BL-519)" section stating the choices BL-519 made inside its decisions, so the ADR is true of the code.

## Context

- BL-519 could not edit `Documentation/Planning/Decisions` while BL-579 held it; its choices are in BL-519's Notes.
- Code: `Curl.Console/ParallelRun.cs`, `ParallelTransferQueue.cs`, `WriteGate.cs`, `RunningTransferState.cs`, `CurlCommandRunner.cs`.

## Acceptance criteria

- [x] ADR-0127 has a section "Implementation (BL-519)" listing each choice under BL-519's Notes "Decisions" heading, each marked "Decided by Claude under Stewart's delegation".
- [x] The ADR index line for 0127 in `Documentation/Planning/Decisions/README.md` still matches the ADR.

## Notes

- Checked each of BL-519's six choices against the code as it is now (`ParallelRun`, `ParallelTransferQueue`, `WriteGate`, `CurlCommandRunner`). Choice 5 had moved on: since BL-648 a transfer takes its `%{conn_id}` at its first `-v` or trace event, else when its report is written, so the ADR says that.
- The README index line for 0127 summarises decisions 1 to 5, which the new section leaves unchanged, so it still matches; no edit needed.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0127 records BL-519's six -Z implementation choices, checked against the code
