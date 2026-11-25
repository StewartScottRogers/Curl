---
id: BL-5993
title: Fix AF-0999: Surviving mutant: the lib582 port can wait 10 s with no sockets and 100 ms with sockets (== -> !=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5993 — Fix AF-0999: Surviving mutant: the lib582 port can wait 10 s with no sockets and 100 ms with sockets (== -> !=)

## Goal

The defect the audit office reported as AF-0999 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0999 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs:177`

Mutant ': readSockets.Count + writeSockets.Count == 0 ? IdleTimeoutMilliseconds' -> '!= 0' survived. The select timeout decision swaps IdleTimeoutMilliseconds (100) and SelectTimeoutMilliseconds (10_000): with no socket to watch the port waits 10 s a turn instead of 100 ms, enough to reach the hang timeout. No test checks the timeout passed to select_test.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs line 177` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib582Run.cs line 177`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
