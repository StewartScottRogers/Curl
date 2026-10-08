---
id: BL-1699
title: Fix AF-0056: Three HttpResponseBodyReaderTests fail on the unmutated tree: the length-delimited/read-to-close body is held whole and written once at the end
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1699 — Fix AF-0056: Three HttpResponseBodyReaderTests fail on the unmutated tree: the length-delimited/read-to-close body is held whole and written once at the end

## Goal

The defect the audit office reported as AF-0056 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0056 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0056-three-httpresponsebodyreadertests-fail-on-the-unmu.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:339`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:339`

Run alone, on an idle machine: CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize fails ('Element at index 0 do not match. Expected: 16384 Actual: 40000'); CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize fails (message differs at index 46); CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore fails ('Expected exception of exact type HttpTransferException but no exception was thrown'). In HttpResponseBodyReader.cs lines 326-346 each read goes into a MemoryStream held; output is written only in the finally block (await WriteAsync(output, held.ToArray(), ...)), so the body is no longer written as it arrives in reads of at most ReadSize (16384), which the class's own doc comment (line 13) and curl's 'passed 16384' failure text require. The tests catch it. Because the baseline is red, the Http mutation run left these tests out (excludedTests), together with ExecuteAsync_ServerStallsPastMaxTime_FailsWithExit28AndTheMeasuredMessage, which failed only during the loaded baseline.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "Name=CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize|Name=CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore|Name=CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize"
```

- Expected: Passed: 3.
- Actual: Failed: 3, Passed: 0 (reads 40000 instead of 16384; no exception where the output fails).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
