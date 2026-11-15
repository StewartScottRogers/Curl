---
id: BL-5217
title: Fix AF-0768: Surviving mutant: a NATS publish of exactly max_payload bytes can be refused with exit 63
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5217 — Fix AF-0768: Surviving mutant: a NATS publish of exactly max_payload bytes can be refused with exit 63

## Goal

The defect the audit office reported as AF-0768 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0768 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83`

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs:83`

Mutant 'payload.Length <= maxPayload' -> '<' survived. The only test, ExecuteAsync_DataOverMaxPayload_FailsWithExit63BeforePublishing, sends 9 bytes to a server whose max_payload is 8; no test publishes exactly max_payload bytes. The mutant refuses the largest allowed payload with exit 63 and sends nothing.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs line 83` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs line 83`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: c50ca152 9742157d.
