---
id: AF-0395
title: TlsReader.Take has no bounds check, so truncated TLS input throws and about 30 fast tests across three projects fail unmutated
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Tls.UnitLibrary/TlsReader.cs:Take:failing-baseline
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0395 - TlsReader.Take has no bounds check, so truncated TLS input throws and about 30 fast tests across three projects fail unmutated

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:126`: TlsReader.Take has no bounds check, so truncated TLS input throws and about 30 fast tests across three projects fail unmutated. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

TlsReader.Take (lines 126-136) is: if (failure.Alert is not null) { Fail(DecodeError); return false; } position += count; return true; - it never checks count against the bytes remaining, so ReadUnsigned goes on to read buffer[index] past the end (line 119) and throws instead of recording DecodeError. Running dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" in a scratch worktree of this commit failed about 30 tests, e.g. 'A list length cut short', 'An empty config', 'A list length past the end', ATruncatedBodyIsADecodeError, DecodeAnswersATruncatedBodyWithDecodeError (8 rows), AMalformedServerHelloIsADecodeError, AMalformedCertificateIsADecodeError, ANewSessionTicketRoundTripsItsLifetimeAndTicket. The same root cause shows in other twins: Invoke-MutationTest -ExcludeBaselineFailures left out Curl.Networking.HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello (stack: TlsReader.ReadUnsigned line 119 <- EchConfigList.ReadConfigs <- EchOffer.Decoded line 126), and Curl.Conformance.Tls13ServerStreamTests.Answer_NoSchemeTheKeyCanMake_IsRefused, Answer_NoX25519Share_IsRefused and FindTls13OnlyClientHello_ReadsOnlyAWholeTls13OnlyClientHello. A malformed ECHConfigList from DNS or a truncated server handshake message therefore escapes as an exception instead of curl's decode failure (exit 35 / plain hello). Red baseline tests also stop the mutation tester from using them, which is why AF-0376 could not be judged.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Passed!: every fast test of Curl.Tls.UnitTests passes.
- Actual: Failed!: about 30 tests fail, e.g. 'A list length cut short', ATruncatedBodyIsADecodeError, AMalformedServerHelloIsADecodeError, each throwing from TlsReader.ReadUnsigned (TlsReader.cs:119).

## Re-audits

## Log

- 2026-10-23: filed proposed.
