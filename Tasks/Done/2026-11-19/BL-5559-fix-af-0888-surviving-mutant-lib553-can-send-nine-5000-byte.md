---
id: BL-5559
title: Fix AF-0888: Surviving mutant: lib553 can send nine 5000-byte headers instead of upstream's eight
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib553.cs, Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5559 — Fix AF-0888: Surviving mutant: lib553 can send nine 5000-byte headers instead of upstream's eight

## Goal

The defect the audit office reported as AF-0888 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0888 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44`

Mutant for (int i = 0; i < 8; i++) -> for (int i = 0; i <= 8; i++) in TestAsync survived all Curl.Conformance.LibTest.UnitTests (seed 446316). It adds a Header8 line of 5000 bytes to the request lib553 sends, so its request bytes no longer match upstream's lib553.c. This is the conformance harness's libtest program, so the bug reaches the upstream-case ratchet rather than curl's users, but its own unit tests do not pin the header count.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs line 44` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs line 44`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by Lib553Tests.RunAsync_SendsEightFiveThousandByteHeadersAndAnEmptyExpect (new, in Curl.Conformance.LibTest.UnitTests/Lib539To553Tests.cs; touches widened to it). Verified by hand: with i <= 8 it fails, reverted it passes (1228 tests green). No production code changed.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: a4b597cb 9742157d.
- 2026-11-19: Doing -> Done. Added Lib553Tests header-count test; mutant killed
