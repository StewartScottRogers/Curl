---
id: BL-5774
title: Fix AF-0977: Surviving mutant: an amqp:// URL with explicit port 0 can be dialled on the default port 5672 (>= -> >)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5774 — Fix AF-0977: Surviving mutant: an amqp:// URL with explicit port 0 can be dialled on the default port 5672 (>= -> >)

## Goal

The defect the audit office reported as AF-0977 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0977 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41`

Location: `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs:41`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp0.UnitLibrary -MaxMutants 25 -Seed 891023: survived. Mutated 'int port = url.Port >= 0 ? url.Port : useTls ? SecureDefaultPort : DefaultPort;' to 'url.Port > 0'. CurlUrl accepts port 0 (CurlUrlAuthority.TryParsePort rejects only values past MaximumPort), so amqp://h:0/q/x would connect to 5672 (or 5671) instead of port 0, reaching a different server. ExecuteAsync_Scheme_ConnectsWithTlsAndPortFromConnector has no port-0 row.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs line 41` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp0.UnitLibrary/Client/Amqp0ProtocolHandler.cs line 41`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b79585e4 9742157d.
