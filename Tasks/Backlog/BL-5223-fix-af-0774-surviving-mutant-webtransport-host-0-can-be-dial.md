---
id: BL-5223
title: Fix AF-0774: Surviving mutant: webtransport://host:0/ can be dialled on UDP 443
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5223 — Fix AF-0774: Surviving mutant: webtransport://host:0/ can be dialled on UDP 443

## Goal

The defect the audit office reported as AF-0774 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0774 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35`

Location: `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs:35`

Mutant 'url.Port >= 0 ? url.Port : DefaultPort' -> 'url.Port > 0' survived. Of_NoPort_DialsUdp443AndSendsTheHostAlone and Of_WtField_ChoosesTheModeAndIsNotSent cover no port and :8443 only. Under the mutant an explicit port 0 dials 443, while AuthorityOf (line 37) still writes host:0, so the dial target and the :authority disagree.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs line 35` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.WebTransport.UnitLibrary/Session/WebTransportRequest.cs line 35`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
