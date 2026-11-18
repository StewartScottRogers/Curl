---
id: BL-5369
title: Fix AF-0849: Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitTests/HandBuiltTlsProviderTests.EarlyData.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5369 — Fix AF-0849: Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT

## Goal

The defect the audit office reported as AF-0849 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0849 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372`

session is { MaxEarlyDataSize: > 0 } -> >= 0 survived. A session ticket whose max_early_data_size is 0 (the server forbids early data) would give an early-data protocol, so --tls-earlydata would send the request as 0-RTT data the server did not allow (request bytes and the handshake outcome). No test resumes a session with MaxEarlyDataSize 0.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs line 372` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs line 372`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `AuthenticateAsClientAsync_WithEarlyDataAndASessionThatAllowsNone_OffersNoEarlyData` (HandBuiltTlsProviderTests.EarlyData.cs): with `>= 0` it fails on the early-data info line; reverted, all 4197 fast tests pass. No production change.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 642b1693 9742157d.
- 2026-11-17: Doing -> Done. Added a test that kills the >= 0 mutant; fast tests green
