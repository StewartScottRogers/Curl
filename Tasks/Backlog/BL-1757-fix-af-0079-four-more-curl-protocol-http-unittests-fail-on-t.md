---
id: BL-1757
title: Fix AF-0079: Four more Curl.Protocol.Http.UnitTests fail on the unmutated tree because HttpResponseBodyReader holds the body and writes it only in a finally block
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1757 — Fix AF-0079: Four more Curl.Protocol.Http.UnitTests fail on the unmutated tree because HttpResponseBodyReader holds the body and writes it only in a finally block

## Goal

The defect the audit office reported as AF-0079 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0079 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0079-four-more-curl-protocol-http-unittests-fail-on-the.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:321`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:321`

The mutation baseline (-ExcludeBaselineFailures) left out 7 failing tests: the 3 in AF-0056 plus ExecuteAsync_ServerStallsPastMaxTime_FailsWithExit28AndTheMeasuredMessage (both rows), ExecuteAsync_ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten, CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize and CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed. Lines 321-323 do 'MemoryStream held = new(); Stream destination = output; output = held;', and only the finally block copies held to destination. So a --max-time timeout writes none of the received body ('ASSERT body: expected hello, actual '; 'ASSERT bytes transferred: expected 0, actual 5'), and an output write failure is never thrown ('Expected exception of exact type HttpTransferException but no exception was thrown'). Run alone with --no-build, all of them fail every time, so machine load is not the cause.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~ServerStallsPastMaxTime|Name=CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize|Name=CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed"
```

- Expected: Passed! - Failed: 0
- Actual: Failed! - Failed: 5 (two ServerStallsPastMaxTime_FailsWithExit28 rows, ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten, DecodeContentAndTheOutputFails, OutputFailsOnTheBodyPrefix)

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
