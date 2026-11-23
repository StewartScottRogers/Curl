---
id: BL-5790
title: Fix AF-0988: Surviving mutant: the header store can count every later line as a new request once a final head ended (finalHeadEnded = false -> true)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferHeaderStore.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5790 — Fix AF-0988: Surviving mutant: the header store can count every later line as a new request once a final head ended (finalHeadEnded = false -> true)

## Goal

The defect the audit office reported as AF-0988 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0988 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235`

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:235`

Seeded mutation run: in BeginNextRequestAfterFinalHead, 'finalHeadEnded = false;' -> 'finalHeadEnded = true;' survived. With the mutant the flag never resets, so request++ runs on every later call and curl_easy_header request indexes after the first redirect (or other repeated request) are numbered wrong. The tests cover two responses, where the bug does not show yet.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferHeaderStore.cs line 235` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferHeaderStore.cs line 235`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 327bc833 9742157d.
