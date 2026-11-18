---
id: BL-5502
title: Fix AF-0860: Surviving mutant: the received close_notify event can be reported as sent
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5502 — Fix AF-0860: Surviving mutant: the received close_notify event can be reported as sent

## Goal

The defect the audit office reported as AF-0860 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0860 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51`

Location: `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs:51`

Mutant 'Sent = false,' -> 'Sent = true,' in the static CloseNotifyReceived TlsMessageEvent survived the whole Curl.Networking.UnitTests run (mutation-Curl.Networking.UnitLibrary.json, seed 435403). Line 146 reports the event with events.ReportTlsMessage(CloseNotifyReceived), and the verbose writer renders Sent as (OUT) instead of (IN). So curl -v would print the server's close_notify as one curl sent, which changes stderr bytes, and no test pins the direction.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs line 51` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/SessionTicketRecordDetector.cs line 51`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
