---
id: BL-5240
title: Fix AF-0789: SegmentedDownload's not-a-plain-GET test records only the last request, so a split POST/PUT/upload goes unseen
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.SegmentedDownload.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5240 — Fix AF-0789: SegmentedDownload's not-a-plain-GET test records only the last request, so a split POST/PUT/upload goes unseen

## Goal

The defect the audit office reported as AF-0789 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0789 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs:196`

Location: `Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs:196`

Step 3 found two surviving mutants of SegmentableRequest.Is (Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs:12, seed 5851): 'Http && IsPlainGet && SendsNoBody || AsksForTheWholeResource' and 'Http && IsPlainGet || SendsNoBody && AsksForTheWholeResource'. Both make --segments split a POST with -d, a PUT, an upload or a transfer with no HTTP options into ranged requests: request bytes change (Range: bytes=0-0 and ranged copies of a POST). The test meant to catch this sets sent = request on every call (line 196) and asserts Assert.AreSame(context, sent) (line 200). Its origin answers TransferResult.Success(0) with no report, so the hook falls back to running the original context last and sent ends as the context either way. The first-occurring && on that line is unassignable when mutated (stillborn), so the targeted -Site run cannot reach these mutants. Ran: Invoke-MutationTest.ps1 -Site ...SegmentableRequest.cs:12:&& -> outcome stillborn.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs -Pattern 'sent = request;|Assert.AreSame\(context, sent\)'
```

- Expected: The test checks every request the hook sent, e.g. that the inner handler was called exactly once with the context itself.
- Actual: Line 196 overwrites sent on every call and line 200 checks only the last one; the sampled mutants SegmentableRequest.cs:12 (&& -> ||, twice) survived.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
