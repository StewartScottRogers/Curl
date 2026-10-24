---
id: BL-3442
title: Fix AF-0395: TlsReader.Take has no bounds check, so truncated TLS input throws and about 30 fast tests across three projects fail unmutated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3442 — Fix AF-0395: TlsReader.Take has no bounds check, so truncated TLS input throws and about 30 fast tests across three projects fail unmutated

## Goal

The defect the audit office reported as AF-0395 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0395 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0395-tlsreader-take-has-no-bounds-check-so-truncated-tl.md`.

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

TlsReader.Take (lines 126-136) is: if (failure.Alert is not null) { Fail(DecodeError); return false; } position += count; return true; - it never checks count against the bytes remaining, so ReadUnsigned goes on to read buffer[index] past the end (line 119) and throws instead of recording DecodeError. Running dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration" in a scratch worktree of this commit failed about 30 tests, e.g. 'A list length cut short', 'An empty config', 'A list length past the end', ATruncatedBodyIsADecodeError, DecodeAnswersATruncatedBodyWithDecodeError (8 rows), AMalformedServerHelloIsADecodeError, AMalformedCertificateIsADecodeError, ANewSessionTicketRoundTripsItsLifetimeAndTicket. The same root cause shows in other twins: Invoke-MutationTest -ExcludeBaselineFailures left out Curl.Networking.HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello (stack: TlsReader.ReadUnsigned line 119 <- EchConfigList.ReadConfigs <- EchOffer.Decoded line 126), and Curl.Conformance.Tls13ServerStreamTests.Answer_NoSchemeTheKeyCanMake_IsRefused, Answer_NoX25519Share_IsRefused and FindTls13OnlyClientHello_ReadsOnlyAWholeTls13OnlyClientHello. A malformed ECHConfigList from DNS or a truncated server handshake message therefore escapes as an exception instead of curl's decode failure (exit 35 / plain hello). Red baseline tests also stop the mutation tester from using them, which is why AF-0376 could not be judged.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Passed!: every fast test of Curl.Tls.UnitTests passes.
- Actual: Failed!: about 30 tests fail, e.g. 'A list length cut short', ATruncatedBodyIsADecodeError, AMalformedServerHelloIsADecodeError, each throwing from TlsReader.ReadUnsigned (TlsReader.cs:119).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
