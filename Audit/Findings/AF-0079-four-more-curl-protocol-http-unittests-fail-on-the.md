---
id: AF-0079
title: Four more Curl.Protocol.Http.UnitTests fail on the unmutated tree because HttpResponseBodyReader holds the body and writes it only in a finally block
auditor: quality
severity: High
status: accepted
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:HeldBodyBaselineFailures:failing-test
reproduction: none
task: BL-1757
tasks: BL-1757
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0079 - Four more Curl.Protocol.Http.UnitTests fail on the unmutated tree because HttpResponseBodyReader holds the body and writes it only in a finally block

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:321`: Four more Curl.Protocol.Http.UnitTests fail on the unmutated tree because HttpResponseBodyReader holds the body and writes it only in a finally block.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:321`

The mutation baseline (-ExcludeBaselineFailures) left out 7 failing tests: the 3 in AF-0056 plus ExecuteAsync_ServerStallsPastMaxTime_FailsWithExit28AndTheMeasuredMessage (both rows), ExecuteAsync_ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten, CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize and CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed. Lines 321-323 do 'MemoryStream held = new(); Stream destination = output; output = held;', and only the finally block copies held to destination. So a --max-time timeout writes none of the received body ('ASSERT body: expected hello, actual '; 'ASSERT bytes transferred: expected 0, actual 5'), and an output write failure is never thrown ('Expected exception of exact type HttpTransferException but no exception was thrown'). Run alone with --no-build, all of them fail every time, so machine load is not the cause.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~ServerStallsPastMaxTime|Name=CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize|Name=CopyAsync_OutputFailsOnTheBodyPrefix_ReportsTheWholePrefixAsPassed"
```

- Expected: Passed! - Failed: 0
- Actual: Failed! - Failed: 5 (two ServerStallsPastMaxTime_FailsWithExit28 rows, ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten, DecodeContentAndTheOutputFails, OutputFailsOnTheBodyPrefix)

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | not re-audited | overlaps planted defect PD-203 in Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran the dotnet test reproduction: Failed! - Failed: 5, Passed: 3, Total: 8. ExecuteAsync_ServerStallsPastMaxTime_ReportsTheHeadAndTheBodyWritten has 'ASSERT body: expected hello, actual ' (empty), and CopyAsync_DecodeContentAndTheOutputFails_ThrowsExit23WithTheEncodedSize: 'no exception was thrown' (HttpResponseBodyReaderTests.cs:407).
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the dotnet test reproduction (after the AF-0056 build): Passed! Failed 0, Passed 8, Total 8.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
