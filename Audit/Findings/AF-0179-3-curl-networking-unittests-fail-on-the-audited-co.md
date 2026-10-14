---
id: AF-0179
title: 3 Curl.Networking.UnitTests fail on the audited commit: TlsReader.Take has no bounds check, so a truncated ECHConfigList throws IndexOutOfRangeException
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Tls.UnitLibrary/TlsReader.cs:Take:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0179 - 3 Curl.Networking.UnitTests fail on the audited commit: TlsReader.Take has no bounds check, so a truncated ECHConfigList throws IndexOutOfRangeException

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:126`: 3 Curl.Networking.UnitTests fail on the audited commit: TlsReader.Take has no bounds check, so a truncated ECHConfigList throws IndexOutOfRangeException. Reappeared; previously AF-0112. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

Invoke-MutationTest.ps1's unmutated baseline for Curl.Networking.UnitLibrary left out 3 failing tests: HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. A filtered dotnet test run gave 'Failed: 5, Passed: 42' (5 data rows). Every failing row has a DNS lookup returning a malformed list ([0x00,0x05,0xfe] or [0x00,0x04,0xfe,0x0d,0x00,0x00]), and the error is 'System.IndexOutOfRangeException: Index was outside the bounds of the array. at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:119) <- ReadUInt16 <- EchConfigList.ReadConfigs (EchConfigList.cs:42) <- EchConfigList.Decode <- EchOffer.Decoded <- EchOffer.DecideAsync <- HandBuiltTlsProvider.PrepareWithEchAsync'. TlsReader.Take(int count) checks only 'failure.Alert is not null' and then does 'position += count; return true;'. It never checks count against 'end - position', so a length prefix past the end of a vector reads outside it and indexes past the buffer instead of recording TlsAlertDescription.DecodeError. A malformed peer- or DNS-supplied structure therefore crashes the transfer with an unhandled exception instead of curl's behaviour: a plain hello for --ech true, or exit 35 for --ech hard. The tests catch it: they are red on the audited commit.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch"
```

- Expected: All 47 tests pass; a truncated ECHConfigList decodes as DecodeError.
- Actual: Failed!  - Failed: 5, Passed: 42, Total: 47; System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:119).

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | not re-audited | overlaps planted defect PD-103 in Curl.Tls.UnitLibrary/TlsReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch": Failed 5, Passed 42, Total 47. The failures are 3 test methods (WithEch_WritesCurlsEchLinesBeforeTheHello x3 rows, WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, WithEchTrueAndNoUsableList_SendsAPlainHello), each System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:119) via EchConfigList.ReadConfigs. TlsReader.Take (line 126) still advances position without a bounds check.

## Log

- 2026-10-13: filed proposed.
