---
id: BL-5737
title: Fix AF-0966: Surviving mutant: removing a handle from a multi can drop every other handle's done message and keep its own (== -> !=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5737 — Fix AF-0966: Surviving mutant: removing a handle from a multi can drop every other handle's done message and keep its own (== -> !=)

## Goal

The defect the audit office reported as AF-0966 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0966 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136`

Location: `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs:136`

Seeded sample (seed 51850, -MaxMutants 40): the mutant at ManagedMultiTransfer.Queueing.cs:136 operator '==' survived. Original: 'messages.RemoveAll(message => message.Transfer == entry.Transfer);'; mutated: '!='. RemoveEntry runs for curl_multi_remove_handle (ManagedMultiTransfer.Entries.cs:96). With the mutant, removing one handle deletes the pending CURLMSG_DONE messages of every other handle and keeps the removed handle's own. curl_multi_info_read then reports a handle that is no longer added and loses the others. No multi test removes one handle while another handle's message is still unread.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs line 136` as the evidence above describes (the mutant operator is `==`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedMultiTransfer.Queueing.cs line 136`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 3b0aeef4 9742157d.
