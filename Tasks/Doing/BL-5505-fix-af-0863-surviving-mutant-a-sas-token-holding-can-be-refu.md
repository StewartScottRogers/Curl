---
id: BL-5505
title: Fix AF-0863: Surviving mutant: a SAS token holding '~' can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5505 — Fix AF-0863: Surviving mutant: a SAS token holding '~' can be refused

## Goal

The defect the audit office reported as AF-0863 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0863 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78`

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs:78`

Mutant 'character is <= ' ' or '#' or > '~'' -> '>= '~'' survived. '~' is an unreserved query character. With the mutant, IsSignedQuery rejects any SAS token containing it, so a valid token is refused as an invalid SAS (a changed refusal and exit code). No test passes a SAS with '~', so the upper bound is unchecked.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs line 78` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageRequestAuthorizer.cs line 78`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: c17fde10 9742157d.
