---
id: BL-5652
title: Fix AF-0917: Surviving mutant: StreamConnection counts a zero-byte (end-of-stream) read as having read bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/StreamConnection.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5652 — Fix AF-0917: Surviving mutant: StreamConnection counts a zero-byte (end-of-stream) read as having read bytes

## Goal

The defect the audit office reported as AF-0917 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0917 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:128`

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:128`

`_hasReadBytes |= read > 0;` mutated to `read >= 0` survived Curl.Networking.UnitTests. _hasReadBytes gates whether an unread FIN at the last write keeps a later WSAECONNABORTED as an abort or reports it as ConnectionReset (ADR-0878, BL-5259: an unread FIN is not counted until the connection has read a byte). With the mutant a fresh connection whose only read returned 0 counts as having read, so the reset/abort decision, and the exit code and message curl prints, change on Windows. No test reads a zero-byte end before the write that decides it.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/StreamConnection.cs line 128` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/StreamConnection.cs line 128`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `ReadAsync_WhenTheOnlyEarlierReadReturnedZeroBytesAndTheFinWasUnreadAtTheWrite_FailsWithConnectionReset` (StreamConnectionTests): with `read >= 0` it fails, with `read > 0` all 4395 fast tests pass. Test-only change; library unchanged.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 8433f1c8 9742157d.
- 2026-11-20: Doing -> Done. Mutant killed by new StreamConnectionTests zero-byte-read test
