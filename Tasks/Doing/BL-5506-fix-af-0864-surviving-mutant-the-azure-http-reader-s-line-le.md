---
id: BL-5506
title: Fix AF-0864: Surviving mutant: the Azure HTTP reader's line-length limit boundary is untested
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5506 — Fix AF-0864: Surviving mutant: the Azure HTTP reader's line-length limit boundary is untested

## Goal

The defect the audit office reported as AF-0864 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0864 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29`

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29`

Mutant 'if (line.WrittenCount + take > MaxLineLength)' -> '>=' survived. A response line of exactly MaxLineLength (65536) bytes would be refused as LineTooLong instead of read. The only line-length test (SendAsync_LineLongerThanTheLimit_FailsLineTooLong) uses a line far past the limit, so the boundary is unchecked and the transfer's refusal can move.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs line 29` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs line 29`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `SendAsync_LineOfExactlyTheLimitWithItsCarriageReturn_IsRead` (verified by hand with `>=`; reverted). The CR before the LF counts toward the limit, so a 65527-byte value gives a line of exactly 65536. `SendAsync_LineOneByteOverTheLimit_FailsLineTooLong` pins the other side. Production code unchanged. Fast tests: 216 pass in the AzureStorage project.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: f511ce0d 9742157d.
