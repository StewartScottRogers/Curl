---
id: AF-0445
title: RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun asserts exit 25 and 'curl: (25)', so it passes while -T with a missing file exits 25 instead of curl's 26
auditor: quality
severity: Critical
status: proposed
reason:
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0445 - RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun asserts exit 25 and 'curl: (25)', so it passes while -T with a missing file exits 25 instead of curl's 26

## Summary

Critical finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:155`: RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun asserts exit 25 and 'curl: (25)', so it passes while -T with a missing file exits 25 instead of curl's 26. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs:155`

The test is named Exits26, but lines 159-160 assert Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode) (UploadFailed = 25), and lines 167/172 expect the stderr text "curl: (25) " + MultipartFormBodyBuilder.OpenFailedMessage. The product constant it pins, Curl.Console/CurlCommandRunner.cs:581 CannotOpenUploadFileFailure = TransferResult.Failure(CurlExitCode.UploadFailed, ...), has a doc comment (lines 575-576) that says curl 8.21.0 measured exit 26 for -T nosuchfile. The upstream cases agree: test289, test491, test496 and test1673 all expect errorcode 26 and now get 25. So the behaviour the test names is broken in the product today, and the test passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/CurlCommandRunnerUploadTests.cs -Pattern 'Exits26AndStopsTheRun|CurlExitCode.UploadFailed, exitCode|curl: \(25\)'
```

- Expected: The Exits26 test asserts CurlExitCode.ReadError (26) and 'curl: (26) ...'; there is no UploadFailed or '(25)' match in it.
- Actual: CurlCommandRunnerUploadTests.cs:155 RunAsync_UploadFileThatCannotBeOpened_Exits26AndStopsTheRun; :160 Assert.AreEqual((int)CurlExitCode.UploadFailed, exitCode); :167 and :172 'curl: (25) {MultipartFormBodyBuilder.OpenFailedMessage}'

## Re-audits

## Log

- 2026-10-25: filed proposed.
