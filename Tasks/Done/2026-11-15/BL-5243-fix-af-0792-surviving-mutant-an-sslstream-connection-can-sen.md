---
id: BL-5243
title: Fix AF-0792: Surviving mutant: an SslStream connection can send a second close_notify after ShutDownSendingAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5243 — Fix AF-0792: Surviving mutant: an SslStream connection can send a second close_notify after ShutDownSendingAsync

## Goal

The defect the audit office reported as AF-0792 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0792 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:196`

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:196`

Mutant '_tlsEnded = true;' -> '_tlsEnded = false;' in ShutDownSendingAsync survived (seed 5851). The flag tells DisposeAsync that close_notify was already sent. Without it, disposing after ShutDownSendingAsync tries to shut TLS down again over a send side already closed, putting another alert on the wire or failing. The hand-built connection has this test (CloseNotifyOnDisposeTests.DisposeAsync_OfAHandBuiltTlsConnectionAfterSendingWasShutDown_TriesNoSecondCloseNotify); SslStreamConnection has none.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/SslStreamConnection.cs line 196` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/SslStreamConnection.cs line 196`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Killed by `CloseNotifyOnDisposeTests.DisposeAsync_OfAnSslStreamConnectionAfterSendingWasShutDown_TriesNoSecondCloseNotify` (mutant applied by hand: it failed; reverted: 4158 fast tests pass). Test-only change; no production code changed.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: 1117eb11 9742157d.
- 2026-11-15: Doing -> Done. SslStreamConnection ShutDownSendingAsync _tlsEnded mutant now killed by a test
