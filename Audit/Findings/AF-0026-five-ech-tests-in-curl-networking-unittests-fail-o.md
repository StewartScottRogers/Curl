---
id: AF-0026
title: Five ECH tests in Curl.Networking.UnitTests fail on the unmutated tree, which blocks mutation testing of Curl.Networking.UnitLibrary
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/EchTlsClientTests.cs:AuthenticateAsClientAsync_WithEch:failing-baseline
reproduction: none
task: BL-1361
tasks: BL-1361
found: 2026-10-03
found-at: 454d1d2abbb96213c945e91f0dc3d241bd40cc2d
scorecard: 2026-10-03_0623.md
duplicate-of:
closed:
closed-how:
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
- 2026-10-07 | 2026-10-07_0844.md | not re-audited | overlaps planted defect PD-103 in Curl.Tls.UnitLibrary/TlsReader.cs (the bounds check in TlsReader.Take removed: the IndexOutOfRangeException at TlsReader.cs:98), so the auditor's verdict (reproduces yes) is set aside and is not a reproduction (corrected 2026-10-07, ADR-0422): Ran the reproduction: 'Failed! - Failed: 5, Passed: 3038, Skipped: 30, Total: 3073'. The failures are AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, ..._WithEchTrueAndNoUsableList_SendsAPlainHello and three rows of ..._WithEch_WritesCurlsEchLinesBeforeTheHello. All throw System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:98), reached from EchConfigList.ReadConfigs (EchConfigList.cs:42). TlsReader.Take (TlsReader.cs:111-121) advances the position with no check against the buffer length, so malformed input throws instead of becoming a DecodeError. Curl.Tls.UnitTests is red too (ATruncatedBodyIsADecodeError, DecodeAnswersATruncatedBodyWithDecodeError rows and more). The Networking mutation baseline therefore still fails, and no mutants can be run.
- 2026-10-07 | 2026-10-07_1336.md | not re-audited | overlaps planted defect PD-101 in Curl.Cryptography.UnitLibrary/AeadChaCha20Poly1305.cs, so the auditor's verdict (reproduces no) is set aside: Ran dotnet test Curl.Networking.UnitTests -c Release: Failed 1, Passed 3034, Skipped 28. None of the failures is an ECH test; all ECH tests pass. The one failure is ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures, a different defect ('Could not Resolve host'), filed separately this audit.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
