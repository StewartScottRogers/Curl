---
id: AF-0463
title: ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:FirstTransferSocket:stale-adr
reproduction: none
task: BL-4225
tasks: BL-4225
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0463 - ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:20`: ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:20`

ADR-0986 Decision line 20: "A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number and the 10 ms timer". Neither `FirstTransferSocket` nor `RunningTransferCheckInterval` appears in any non-test .cs file. Curl.Engine.UnitLibrary/ManagedMultiTransfer.Timer.cs TimerMilliseconds/RunningTimerMilliseconds returns 0 for a running transfer with no known socket, and WaitedSockets (ManagedMultiTransfer.WaitSockets.cs:136-147) leaves such a transfer out. ADR-0986's status line is still plain "Accepted", with no note that ADR-0997 and ADR-0998 replaced this part.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0986-*.md -Pattern 'keeps the synthetic `FirstTransferSocket`'; (Get-ChildItem -Recurse -Filter *.cs | Where-Object FullName -notmatch 'Tests' | Select-String -Pattern 'FirstTransferSocket|RunningTransferCheckInterval').Count
```

- Expected: Either the ADR is marked replaced in part, or the code still has FirstTransferSocket (count > 0).
- Actual: ADR-0986-...md:20: - A fake that wraps no engine transfer keeps the synthetic `FirstTransferSocket` number and the 10 ms timer: it; count 0

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
