---
id: BL-2568
title: Fix AF-0231: TlsReader.ReadVector does not check a vector's length against the bytes left: 23 Curl.Tls and 3 Networking ECH tests fail unmutated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2568 — Fix AF-0231: TlsReader.ReadVector does not check a vector's length against the bytes left: 23 Curl.Tls and 3 Networking ECH tests fail unmutated

## Goal

The defect the audit office reported as AF-0231 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0231 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0231-tlsreader-readvector-does-not-check-a-vector-s-len.md`.

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:76`

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:76`

ReadVector reads the length, then does 'position += length; return new TlsReader(buffer, start, position, failure);' without Take(length), so a vector longer than the input gives a sub-reader whose end is past the buffer. ReadUnsigned then indexes past the array. Invoke-MutationTest's baseline on Curl.Networking.UnitTests left out 3 failing tests (HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, _WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, _WithEchTrueAndNoUsableList_SendsAPlainHello). Rerunning them in a git-archive copy of the tree gave System.IndexOutOfRangeException at TlsReader.ReadUnsigned (TlsReader.cs:120) <- EchConfigList.ReadConfigs <- EchOffer.Decoded, for the DNS list [0x00,0x05,0xfe]. Curl.Tls.UnitTests in the same copy: Failed 23, Passed 1252 (EchConfigListTests.AMalformedListIsADecodeError, HandshakeMessageRejectionTests.DecodeAnswersATruncatedBodyWithDecodeError, Tls12ClientHandshakeFailureTests.AMalformedCertificateIsADecodeError and others). Users see it: a truncated ECH config list or handshake message throws an unhandled exception instead of decode_error and exit 35.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Passed! - Failed: 0.
- Actual: Failed! - Failed: 23, Passed: 1252, Total: 1275; IndexOutOfRangeException at TlsReader.ReadUnsigned.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
