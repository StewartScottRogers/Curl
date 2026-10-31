---
id: AF-0463
title: ADR-0986 (Accepted, no amendment note) still says a fake transfer keeps the FirstTransferSocket number and the 10 ms timer, both removed
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-31_1149.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-31_0358.md, 2026-10-31_1149.md).
key: truthfulness:Documentation/Planning/Decisions/ADR-0986-wrapped-transfers-run-on-a-socket-wait-and-computing-ones-ask-for-timer-0.md:FirstTransferSocket:stale-adr
reproduction: none
task: BL-4225
tasks: BL-4225
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: consecutive
closed-by: 2026-10-31_0358.md, 2026-10-31_1149.md
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

- 2026-10-31 | 2026-10-31_0358.md | reproduces: no | Ran the reproduction. ADR-0986 no longer matches 'keeps the synthetic `FirstTransferSocket`'. Its status line now says 'Its second Decision point is replaced by ADR-0997 and ADR-0998: `FirstTransferSocket` and `RunningTransferCheckInterval` are gone.' The non-test .cs count for FirstTransferSocket|RunningTransferCheckInterval is 0.
- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the reproduction: 'keeps the synthetic `FirstTransferSocket`' no longer matches ADR-0986, and the production-code count of FirstTransferSocket|RunningTransferCheckInterval is 0. ADR-0986 line 4 now says it is replaced by ADR-0997 and ADR-0998 and that both names are gone.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-31_0358.md, 2026-10-31_1149.md).
