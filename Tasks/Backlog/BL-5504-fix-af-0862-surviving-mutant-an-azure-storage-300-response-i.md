---
id: BL-5504
title: Fix AF-0862: Surviving mutant: an Azure Storage 300 response is taken as success
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5504 — Fix AF-0862: Surviving mutant: an Azure Storage 300 response is taken as success

## Goal

The defect the audit office reported as AF-0862 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0862 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60`

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60`

Mutant 'if (response.StatusCode >= 300)' -> 'if (response.StatusCode > 300)' survived Curl.Extended.Protocol.AzureStorage.UnitTests (mutation-Curl.Extended.Protocol.AzureStorage.UnitLibrary.json). A 300 Multiple Choices reply would then be read as the object (written to stdout, exit 0) instead of writing the error body and failing with exit 22 through ErrorOf. That changes the exit code and the output bytes, and no test uses status 300.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs line 60` as the evidence above describes (the mutant operator is `>=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs line 60`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-18: Created.
