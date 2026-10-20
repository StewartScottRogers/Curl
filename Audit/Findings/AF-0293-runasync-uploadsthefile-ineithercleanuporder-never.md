---
id: AF-0293
title: RunAsync_UploadsTheFile_InEitherCleanupOrder never checks that the file was uploaded
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs:RunAsync_UploadsTheFile_InEitherCleanupOrder:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0293 - RunAsync_UploadsTheFile_InEitherCleanupOrder never checks that the file was uploaded

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs:12`: RunAsync_UploadsTheFile_InEitherCleanupOrder never checks that the file was uploaded.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs:12`

Step 1 sample. The test writes "Moooooooooooo\n" to a temporary file, runs Lib525 for 525 and 529, and asserts only result.ExitCode == 0 and ProgramName == "lib525". The FakeCurlEngine has no Performing hook, so nothing reads the upload callback or checks the bytes, the upload flag or the file size. A port that never set up the upload would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs -Pattern 'RunAsync_UploadsTheFile_InEitherCleanupOrder' -Context 0,12
```

- Expected: The body asserts the uploaded bytes (or the upload options) the name promises.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.AreEqual("lib525", ...ProgramName).

## Re-audits

## Log

- 2026-10-19: filed proposed.
