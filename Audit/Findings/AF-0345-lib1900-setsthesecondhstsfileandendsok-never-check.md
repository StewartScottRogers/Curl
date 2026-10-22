---
id: AF-0345
title: Lib1900_SetsTheSecondHstsFileAndEndsOk never checks that any HSTS file is set
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:Lib1900_SetsTheSecondHstsFileAndEndsOk:name-lies
reproduction: none
task: BL-3125
tasks: BL-3125
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0345 - Lib1900_SetsTheSecondHstsFileAndEndsOk never checks that any HSTS file is set

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:254`: Lib1900_SetsTheSecondHstsFileAndEndsOk never checks that any HSTS file is set.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:254`

The body runs Lib1900 over a FakeCurlEngine and asserts only result.ExitCode == 0 and an empty standard error. Lib1900.TestAsync sets CURLOPT_HSTS twice (first-hsts.txt, then second-hsts.txt) and duplicates the handle, but the test never reads which HSTS file the handle (or its copy) holds. Dropping the second CURLOPT_HSTS, or keeping the first, would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs -Pattern 'Lib1900_SetsTheSecondHstsFileAndEndsOk' -Context 0,10
```

- Expected: The body asserts the handle's Hsts option is second-hsts.txt.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.AreEqual(string.Empty, run.Error.ToString()).

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
