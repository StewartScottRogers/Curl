---
id: BL-5301
title: Fix AF-0827: Surviving mutant: a QUIC dial whose TLS setup fails can go on to the next address
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5301 — Fix AF-0827: Surviving mutant: a QUIC dial whose TLS setup fails can go on to the next address

## Goal

The defect the audit office reported as AF-0827 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0827 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:197`

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:197`

Mutation sample (seed 413266): '? (preparationFailure!, false)' mutated to '? (preparationFailure!, true)' survived. The bool is the attempt's TryNextAddress (QuicDialer.cs:182). With the mutant, a TLS preparation failure (a bad --cert, an unusable cipher list), which cannot succeed on any address, makes the dialer try every next address: one more 'Trying' line and failed attempt per address under -v. No test dials a host with two addresses and a TLS setup failure.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/QuicDialer.cs line 197` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/QuicDialer.cs line 197`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `ConnectMultiplexedAsync_WithACertFileThatDoesNotExistAndTwoAddresses_StopsAtTheFirstWithoutATryingLineForTheSecond` (TcpConnectorQuicTests.TlsOptions.cs): with line 197 mutated it failed (a Trying line for 127.0.0.2); reverted, all 4163 fast tests pass. Test-only change.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 54cfc37b 9742157d.
- 2026-11-16: Doing -> Done. Mutant killed by new two-address cert-failure test
