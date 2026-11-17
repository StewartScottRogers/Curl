---
id: BL-5367
title: Fix AF-0847: Surviving mutant: a QUIC dial with exactly no connect time left still tries the next address
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5367 — Fix AF-0847: Surviving mutant: a QUIC dial with exactly no connect time left still tries the next address

## Goal

The defect the audit office reported as AF-0847 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0847 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:133`

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:133`

if (remaining <= TimeSpan.Zero) -> < TimeSpan.Zero survived. When the connect timeout is exactly used up, the dialer goes on to attempt the address with a zero budget instead of returning TimedOut(request) at once. The clock is injected, so the boundary is testable.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/QuicDialer.cs line 133` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/QuicDialer.cs line 133`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by the new test `TcpConnectorQuicTests.ConnectMultiplexedAsync_WhenTheFirstAddressUsesUpExactlyTheConnectTimeout_FailsWithExit28WithoutTryingTheNextAddress`: with `<` at QuicDialer.cs:133 it failed (exit 7 instead of 28), reverted it passes. Production code unchanged. Build clean, 4198 fast tests in Curl.Networking.UnitTests pass.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: abdd7c5c 9742157d.
- 2026-11-17: Doing -> Done. Added test pinning the exact-zero-remaining boundary; mutant < killed, reverted passes
