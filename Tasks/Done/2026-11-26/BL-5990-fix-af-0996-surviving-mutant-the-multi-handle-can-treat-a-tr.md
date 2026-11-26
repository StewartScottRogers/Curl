---
id: BL-5990
title: Fix AF-0996: Surviving mutant: the multi handle can treat a transfer whose deadline is exactly now as still settled (<= -> <)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs, Curl.Engine.UnitTests/ManagedMultiTransferTests.DeadlineAtNow.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5990 — Fix AF-0996: Surviving mutant: the multi handle can treat a transfer whose deadline is exactly now as still settled (<= -> <)

## Goal

The defect the audit office reported as AF-0996 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0996 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs:273`

Mutant '!(entry.SocketWait!.EarliestDeadline <= timeProvider.GetUtcNow())' -> '!(... < ...)' survived. IsSettled's doc says a transfer whose deadline has passed is let go on until its own timer fails it with OperationTimedOut (CGR-034 C_75); with the mutant a deadline equal to the clock is not yet passed, so a socket_action at exactly the deadline tick does not let the timeout run. Tests use manual clocks but none stops at the exact deadline.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs line 273` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.MovingOn.cs line 273`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killing test: `ManagedMultiTransferTests.SocketActionAsync_DeadlineExactlyNow_LetsTheTransferGoOnUntilItTimesOut`
  (new file `Curl.Engine.UnitTests/ManagedMultiTransferTests.DeadlineAtNow.cs`, added to `touches`;
  no other task in Doing named it). With `<=` changed to `<` by hand it fails with
  `expected: (Ok, 0)`, `actual: (Ok, 1)`; with `<=` restored the fast tests all pass.
- No production change: line 273 was already right; only the test was missing.
- `TimerManualTimeProvider` moves only timestamps, not `GetUtcNow`, which `IsSettled` reads, so the
  test uses its own clock that stops exactly on the deadline. The transfer ends a real second after
  the action starts: correct code waits for it (up to 30 s), so load cannot fail the test; the
  mutant returns at once with the transfer still running.
- Pipeline `feature` collapsed to writing the one test in the session: no behaviour, design or
  document changes, so no ADR and no stage agents.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: f9a44c83 9742157d.
- 2026-11-25: Doing -> Done. A fast test now stops the clock exactly on a socket-waiting transfer's deadline and kills AF-0996's <= to < mutant
