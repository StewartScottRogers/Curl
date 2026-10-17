---
id: AF-0231
title: TlsReader.ReadVector does not check a vector's length against the bytes left: 23 Curl.Tls and 3 Networking ECH tests fail unmutated
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Tls.UnitLibrary/TlsReader.cs:ReadVector:baseline-failure
reproduction: none
task: BL-2568
tasks: BL-2568
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0231 - TlsReader.ReadVector does not check a vector's length against the bytes left: 23 Curl.Tls and 3 Networking ECH tests fail unmutated

## Summary

High finding from the quality auditor at `Curl.Tls.UnitLibrary/TlsReader.cs:76`: TlsReader.ReadVector does not check a vector's length against the bytes left: 23 Curl.Tls and 3 Networking ECH tests fail unmutated. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:76`

ReadVector reads the length, then does 'position += length; return new TlsReader(buffer, start, position, failure);' without Take(length), so a vector longer than the input gives a sub-reader whose end is past the buffer. ReadUnsigned then indexes past the array. Invoke-MutationTest's baseline on Curl.Networking.UnitTests left out 3 failing tests (HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, _WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte, _WithEchTrueAndNoUsableList_SendsAPlainHello). Rerunning them in a git-archive copy of the tree gave System.IndexOutOfRangeException at TlsReader.ReadUnsigned (TlsReader.cs:120) <- EchConfigList.ReadConfigs <- EchOffer.Decoded, for the DNS list [0x00,0x05,0xfe]. Curl.Tls.UnitTests in the same copy: Failed 23, Passed 1252 (EchConfigListTests.AMalformedListIsADecodeError, HandshakeMessageRejectionTests.DecodeAnswersATruncatedBodyWithDecodeError, Tls12ClientHandshakeFailureTests.AMalformedCertificateIsADecodeError and others). Users see it: a truncated ECH config list or handshake message throws an unhandled exception instead of decode_error and exit 35.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Passed! - Failed: 0.
- Actual: Failed! - Failed: 23, Passed: 1252, Total: 1275; IndexOutOfRangeException at TlsReader.ReadUnsigned.

## Re-audits

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
