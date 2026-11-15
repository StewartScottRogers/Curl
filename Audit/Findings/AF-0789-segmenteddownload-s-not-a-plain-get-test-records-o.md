---
id: AF-0789
title: SegmentedDownload's not-a-plain-GET test records only the last request, so a split POST/PUT/upload goes unseen
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs:ExecuteAsync_ATransferThatIsNotAPlainGetOfTheWholeResource_RunsAsCurlRunsIt:weak-assertion
reproduction: none
task: BL-5240
tasks: BL-5240
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0789 - SegmentedDownload's not-a-plain-GET test records only the last request, so a split POST/PUT/upload goes unseen

## Summary

High finding from the quality auditor at `Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs:196`: SegmentedDownload's not-a-plain-GET test records only the last request, so a split POST/PUT/upload goes unseen.

## Evidence

Location: `Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs:196`

Step 3 found two surviving mutants of SegmentableRequest.Is (Curl.Extension.SegmentedDownload.UnitLibrary/SegmentableRequest.cs:12, seed 5851): 'Http && IsPlainGet && SendsNoBody || AsksForTheWholeResource' and 'Http && IsPlainGet || SendsNoBody && AsksForTheWholeResource'. Both make --segments split a POST with -d, a PUT, an upload or a transfer with no HTTP options into ranged requests: request bytes change (Range: bytes=0-0 and ranged copies of a POST). The test meant to catch this sets sent = request on every call (line 196) and asserts Assert.AreSame(context, sent) (line 200). Its origin answers TransferResult.Success(0) with no report, so the hook falls back to running the original context last and sent ends as the context either way. The first-occurring && on that line is unassignable when mutated (stillborn), so the targeted -Site run cannot reach these mutants. Ran: Invoke-MutationTest.ps1 -Site ...SegmentableRequest.cs:12:&& -> outcome stillborn.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.SegmentedDownload.UnitTests/SegmentedDownloadTransferHookTests.cs -Pattern 'sent = request;|Assert.AreSame\(context, sent\)'
```

- Expected: The test checks every request the hook sent, e.g. that the inner handler was called exactly once with the context itself.
- Actual: Line 196 overwrites sent on every call and line 200 checks only the last one; the sampled mutants SegmentableRequest.cs:12 (&& -> ||, twice) survived.

## Re-audits

## Log

- 2026-11-15: filed proposed.
