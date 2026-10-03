---
id: AF-0026
title: Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/EchTlsClientTests.cs:AuthenticateAsClientAsync_WithEch:failing-baseline
task: BL-1361
found: 2026-10-03
found-at: 454d1d2abbb96213c945e91f0dc3d241bd40cc2d
scorecard: 2026-10-03_0623.md
closed:
closed-by:
---
# AF-0026 - Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitTests`: Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary. Reported by an auditor flagged unreliable in 2026-10-03_0623.md.

## Evidence

Location: `Curl.Networking.UnitTests`

dotnet test Curl.Networking.UnitTests -c Release gives Failed: 5, Passed: 2929, Skipped: 31. Failing tests include AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, AuthenticateAsClientAsync_WithEchTrueAndNoUsableList_SendsAPlainHello and AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello. Invoke-MutationTest.ps1 stops with baselineError 'the unmutated tests failed or timed out (exit 1)' and score null, so none of the library's survivors can be killed or confirmed. The test file path in the key is a best guess; the failing tests are in Curl.Networking.UnitTests.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo
```

- Expected: Passed! with Failed: 0
- Actual: Failed! - Failed: 5, Passed: 2929, Skipped: 31, Total: 2965

## Re-audits

- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | dotnet test Curl.Networking.UnitTests -c Release: Passed 2973, Failed 0, Skipped 31.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | dotnet test Curl.Networking.UnitTests -c Release: Failed 0, Passed 2974, Skipped 30.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
