---
id: BL-5199
title: Fix AF-0750: Surviving mutant: a gs:// response with status 300 can be taken as success
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5199 — Fix AF-0750: Surviving mutant: a gs:// response with status 300 can be taken as success

## Goal

The defect the audit office reported as AF-0750 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0750 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51`

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs:51`

Mutant 'if (response.StatusCode >= 300)' -> 'if (response.StatusCode > 300)' survived the twin's tests (seed 465187 sample). A 300 Multiple Choices reply would then be read as the object (exit 0 with the error body as output) instead of failing with exit 22. The tests cover 403 and 404 but no 300 boundary.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs line 51` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsClient.cs line 51`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
