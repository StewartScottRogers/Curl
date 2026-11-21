---
id: BL-5690
title: Fix AF-0931: Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5690 — Fix AF-0931: Surviving mutant: a proxy CONNECT that answers a challenge on the same connection can be reported as not answering it

## Goal

The defect the audit office reported as AF-0931 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0931 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1926`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1926`

Seed 812510 sample: answersChallenge = true; mutated to answersChallenge = false; survived (the tool names the member NetworkDiagnosticLog). answersChallenge goes to RequestTunnelAsync and ConnectTunnelVerboseLines.ReportBeforeConnect (TcpConnector.cs:2100), so the repeated CONNECT after a 407 writes other verbose lines and is treated as a first request. No test checks that second CONNECT.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/TcpConnector.cs line 1926` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/TcpConnector.cs line 1926`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by the new test `ConnectAsync_WithProxyAnyAuth_WhenTheType1AnswersAChallengeAndTheNextChallengeIsBare_FailsWithThe407` (TcpConnectorTests.ProxyNtlmAndNegotiate.cs): with line 1926 set to false the repeated NTLM leg is treated as sent before any challenge, so a bare NTLM 407 goes to Negotiate instead of failing with the 407. Verified by hand: fails with the mutant, passes reverted; fast tests of Curl.Networking.UnitTests green (4398). Production code unchanged.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 3fc4315c 9742157d.
- 2026-11-21: Doing -> Done. Test added that kills the mutant
