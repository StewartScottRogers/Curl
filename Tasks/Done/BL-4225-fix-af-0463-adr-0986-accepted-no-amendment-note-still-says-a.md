---
id: BL-4225
title: Fix AF-0463: ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4225 — Fix AF-0463: ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed

## Goal

The defect the audit office reported as AF-0463 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0463 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0463-adr-0986-accepted-no-amendment-note-still-says-a-f.md`.

Location: `Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:20`

Location: `Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:20`

ADR-0986 Decision line 20: "A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number and the 10 ms timer". Neither `FirstTransferSocket` nor `RunningTransferCheckInterval` appears in any non-test .cs file. Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs TimerMilliseconds/RunningTimerMilliseconds returns 0 for a running transfer with no known socket, and WaitedSockets (ManagedMultiTransfer.WaitSockets.cs:136-147) leaves such a transfer out. ADR-0986's status line is still plain "Accepted", with no note that ADR-0997 and ADR-0998 replaced this part.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0986-*.md -Pattern 'keeps the synthetic `FirstTransferSocket`'; (Get-ChildItem -Recurse -Filter *.cs | Where-Object FullName -notmatch 'Tests' | Select-String -Pattern 'FirstTransferSocket|RunningTransferCheckInterval').Count
```

- Expected: Either the ADR is marked replaced in part, or the code still has FirstTransferSocket (count > 0).
- Actual: ADR-0986-...md:20: - A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number and the 10 ms timer: it; count 0

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Done directly rather than through align-and-document: a two-line ADR edit. ADR-0986's status line and README row now say its fake-transfer Decision point is replaced by ADR-0997 and ADR-0998, and that point is rewritten to say what the code does now (told no socket, left out of GetWaitSockets, timer 0). The reproduction's Select-String now finds nothing; the code count stays 0.
- Build clean; fast tests: 63 test projects passed, 0 failed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. ADR-0986 marks its fake-transfer point replaced by ADR-0997 and ADR-0998 and no longer claims FirstTransferSocket or the 10 ms timer
