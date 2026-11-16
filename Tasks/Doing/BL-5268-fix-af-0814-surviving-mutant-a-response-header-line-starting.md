---
id: BL-5268
title: Fix AF-0814: Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferHeaderStore.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5268 — Fix AF-0814: Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store

## Goal

The defect the audit office reported as AF-0814 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0814 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277`

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277`

`if (colon >= 0)` changed to `if (colon > 0)` survived. A line such as ':x' (colon at index 0) is stored as a header with an empty name today; with the mutant it is dropped, changing what curl_easy_header and curl_easy_nextheader return. No test feeds a line with a leading colon.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferHeaderStore.cs line 277` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferHeaderStore.cs line 277`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 64dab6e2 9742157d.
