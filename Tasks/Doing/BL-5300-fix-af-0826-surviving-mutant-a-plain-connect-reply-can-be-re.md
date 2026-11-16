---
id: BL-5300
title: Fix AF-0826: Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/HttpProxyTunnel.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5300 — Fix AF-0826: Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length

## Goal

The defect the audit office reported as AF-0826 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0826 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163`

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163`

Mutation sample (seed 413266): 'ReadReplyAsync(connection, forConnectUdp: false, cancellationToken);' mutated to 'forConnectUdp: true' survived; every Curl.Networking.UnitTests test passed. TcpConnector's plain CONNECT goes through this overload (TcpConnector.cs:2064). forConnectUdp decides IgnoresBodyFields(statusCode, forConnectUdp) => statusCode / 100 == 2 || (forConnectUdp && statusCode == 101). With the mutant, a proxy answering a plain CONNECT with 101 has its Content-Length ignored: the file's own comment says curl exits 8 at a Content-Length that is not a number unless the status ignores it, so the exit code changes. No test pins a CONNECT answered 101.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs line 163` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs line 163`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 7eb2d710 9742157d.
