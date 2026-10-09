---
id: AF-0112
title: Three Curl.Networking.UnitTests ECH tests fail unmutated: TlsReader.Take advances past the end with no bounds check
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-09_0647.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0225.md, 2026-10-09_0647.md).
key: quality:Curl.Tls.UnitLibrary/TlsReader.cs:Take:failing-test
reproduction: none
task: BL-1862
tasks: BL-1862
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0225.md, 2026-10-09_0647.md
---
# AF-0112 - Three Curl.Networking.UnitTests ECH tests fail unmutated: TlsReader.Take advances past the end with no bounds check

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:126`: Three Curl.Networking.UnitTests ECH tests fail unmutated: TlsReader.Take advances past the end with no bounds check. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

The Networking mutation baseline left out 3 tests that fail unmutated: HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. Rerun on the audited tree, each throws System.IndexOutOfRangeException at TlsReader.ReadUnsigned (TlsReader.cs:113) <- ReadUInt16 <- EchConfigList.ReadConfigs (EchConfigList.cs:42) <- EchOffer.Decoded. TlsReader.Take (line 126) only checks for an earlier failure and then does 'position += count; return true;', with no check that position + count stays within the end, so a truncated vector reads past the buffer instead of recording DecodeError. Curl.Tls.UnitTests also fails on the audited tree (e.g. 'A list length cut short', 'ATruncatedBodyIsADecodeError', DecodeAnswersATruncatedBodyWithDecodeError for every handshake type). A malformed ECH config list or TLS message from a peer crashes the transfer instead of failing with curl's exit code.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo --filter "Name=AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello|Name=AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte|Name=AuthenticateAsClientAsync_WithEchTrueAndNoUsableList_SendsAPlainHello"
```

- Expected: Passed! - Failed: 0.
- Actual: Failed! - Failed: 5, Passed: 32, Total: 37; every failure is System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:113).

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the dotnet test reproduction on Curl.Networking.UnitTests: Passed! Failed 0, Passed 37, Total 37 (the three ECH tests' data rows). The sampled mutation baseline of Curl.Networking.UnitTests excluded no tests.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the dotnet test reproduction: 'Passed! - Failed: 0, Passed: 37, Skipped: 0, Total: 37' (the filtered ECH tests with their data rows). The Networking mutation baseline also ran with excludedTests empty.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0647.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0225.md, 2026-10-09_0647.md).
