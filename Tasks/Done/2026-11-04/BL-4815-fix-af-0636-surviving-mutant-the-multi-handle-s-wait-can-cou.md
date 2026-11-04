---
id: BL-4815
title: Fix AF-0636: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4815 — Fix AF-0636: Surviving mutant: the multi handle's wait can count finished or never-started transfers as running, and no test fails

## Goal

The defect the audit office reported as AF-0636 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0636 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0636-surviving-mutant-the-multi-handle-s-wait-can-count.md`.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'entries.FindAll(entry => !entry.Done && entry.Perform is not null)' -> '||'. Done transfers that have a perform, and pending ones not yet started, are counted among the running ones a curl_multi_wait waits on, which changes the wait's sockets and when it returns. No test waits with a finished or pending transfer beside a running one.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedMultiTransfer.WaitSockets.cs:142:&& -Member WaitedSockets -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `GetWaitSockets_FinishedTransferThatHeldASocketBesideARunningOne_IsOnlyTheRunningOnesSocketThenTheResolverWakeup` to `Curl.Engine.UnitTests/ManagedMultiTransferTests.Counts.cs`: a transfer that took socket 0x1236 and finished, reported by a second perform, beside one still holding 0x1237; the wait sockets are only 0x1237 and the resolver wakeup (count 2). A pending transfer has no socket record (set only in `Start`), so the finished one is what tells `&&` from `||`.
- Checked by hand (lanes may not run `Audit/Tools`): with the site mutated to `||` the new test fails (count 3, not 2); restored, all 2973 engine fast tests pass and `dotnet build` is clean. Test-only change, so no quality run was needed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Test pins that a finished transfer's socket is not among the multi wait sockets; the && to || mutant now fails it
