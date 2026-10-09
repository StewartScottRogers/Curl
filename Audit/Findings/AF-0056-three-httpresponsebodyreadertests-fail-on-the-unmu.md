---
id: AF-0056
title: Three HttpResponseBodyReaderTests fail on the unmutated tree: the length-delimited/read-to-close body is held whole and written once at the end
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitTests/HttpResponseBodyReaderTests.cs:HttpResponseBodyReaderTests:failing-test
reproduction: none
task: BL-1699
tasks: BL-1699
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0056 - Three HttpResponseBodyReaderTests fail on the unmutated tree: the length-delimited/read-to-close body is held whole and written once at the end

## Summary

Medium finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:339`: Three HttpResponseBodyReaderTests fail on the unmutated tree: the length-delimited/read-to-close body is held whole and written once at the end.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:339`

Run alone, on an idle machine: CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize fails ('Element at index 0 do not match. Expected: 16384 Actual: 40000'); CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize fails (message differs at index 46); CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore fails ('Expected exception of exact type HttpTransferException but no exception was thrown'). In HttpResponseBodyReader.cs lines 326-346 each read goes into a MemoryStream held; output is written only in the finally block (await WriteAsync(output, held.ToArray(), ...)), so the body is no longer written as it arrives in reads of at most ReadSize (16384), which the class's own doc comment (line 13) and curl's 'passed 16384' failure text require. The tests catch it. Because the baseline is red, the Http mutation run left these tests out (excludedTests), together with ExecuteAsync_ServerStallsPastMaxTime_FailsWithExit28AndTheMeasuredMessage, which failed only during the loaded baseline.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "Name=CopyAsync_LargeReadToCloseBody_ArrivesWholeInReadsOfAtMostTheReadSize|Name=CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore|Name=CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize"
```

- Expected: Passed: 3.
- Actual: Failed: 3, Passed: 0 (reads 40000 instead of 16384; no exception where the output fails).

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the dotnet test command: 'Failed! - Failed: 3, Passed: 0'. LargeReadToCloseBody shows 'ASSERT write sizes: expected 16384, 16384, 7232, actual ' (nothing written in pieces). OutputFailsOnALargeBody shows 'Expected exception of exact type HttpTransferException but no exception was thrown'. Cause: HttpResponseBodyReader.cs:321-351 buffers into 'MemoryStream held' and copies it once in finally.
- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the dotnet test reproduction: Failed! - Failed: 3, Passed: 0, Total: 3. E.g. CopyAsync_OutputFailsAfterAcceptingSomeBytes_ReportsThemAndTheBytesBefore: 'Expected exception of exact type HttpTransferException but no exception was thrown' (HttpResponseBodyReaderTests.cs:215). The same 3 tests were left out as baseline failures by the Http mutation run.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the dotnet test reproduction: Passed! Failed 0, Passed 3, Total 3 (Curl.Protocol.Http.UnitTests). The sampled mutation baseline of Curl.Protocol.Http.UnitTests also excluded no failing tests.
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces no) is set aside: Ran the dotnet test reproduction: 'Passed! - Failed: 0, Passed: 3, Skipped: 0, Total: 3'. The Http mutation baseline also ran with excludedTests empty.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
