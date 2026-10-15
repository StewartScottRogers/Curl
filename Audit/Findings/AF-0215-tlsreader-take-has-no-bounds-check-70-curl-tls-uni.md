---
id: AF-0215
title: TlsReader.Take has no bounds check: 70 Curl.Tls unit tests and 3 Curl.Networking ECH tests fail unmutated with IndexOutOfRangeException
auditor: quality
severity: High
status: rejected
reason: Stewart rejected it on 2026-10-14: it is audit 20261014-181528's planted TlsReader.Take defect, not a defect in the real tree.
key: quality:Curl.Tls.UnitLibrary/TlsReader.cs:Take:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-14
found-at: d86a80f0091fbf88c08d7b9b2fa37a9197854bd8
scorecard: 2026-10-14_1815.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0215 - TlsReader.Take has no bounds check: 70 Curl.Tls unit tests and 3 Curl.Networking ECH tests fail unmutated with IndexOutOfRangeException

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:126`: TlsReader.Take has no bounds check: 70 Curl.Tls unit tests and 3 Curl.Networking ECH tests fail unmutated with IndexOutOfRangeException. Reappeared; previously AF-0112. Reported by an auditor flagged unreliable in 2026-10-14_1815.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

TlsReader.Take(int count) only checks for an earlier failure, then does 'position += count; return true;'. It never checks that position + count fits the buffer. ReadUnsigned (line 119) then indexes past the end, so a truncated TLS structure throws IndexOutOfRangeException. Curl.Tls's contract (its CLAUDE.md) is that decoders return decode_error and never throw. dotnet test Curl.Tls.UnitTests: 'Failed: 70, Passed: 1201, Total: 1271'. The failures include DecodeAnswersATruncatedBodyWithDecodeError (ClientHello, ServerHello, Certificate, CertificateVerify and others), ServerName_EmptyData_IsDecodeError, KeyShareRoundTripsTheClientShares and DecodeResult_ValueOfAFailure_ThrowsInvalidOperationNamingTheAlert. The Curl.Networking mutation baseline also had to exclude AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. Their stack ends in TlsReader.ReadUnsigned <- EchConfig.Decode (EchConfig.cs:53) <- EchConfigList.ReadConfigs <- EchOffer.Decoded. A server or DNS record sending a short TLS structure would crash the transfer instead of failing with curl's exit code.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Passed! - Failed: 0
- Actual: Failed! - Failed: 70, Passed: 1201, Skipped: 0, Total: 1271 (System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned, TlsReader.cs:line 119)

## Re-audits

## Log

- 2026-10-14: filed proposed.
- 2026-10-14: proposed -> rejected.
