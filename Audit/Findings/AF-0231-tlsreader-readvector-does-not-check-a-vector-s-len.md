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

- 2026-10-18 | 2026-10-18_1045.md | not re-audited | overlaps planted defect PD-103 in Curl.Tls.UnitLibrary/TlsReader.cs, so the auditor's verdict (reproduces yes) is set aside: Ran dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration": Failed: 70, Passed: 1205, Total: 1275. Examples: EchConfigListTests.AMalformedListIsADecodeError throws ArgumentOutOfRangeException and IndexOutOfRangeException, and HandshakeMessageRejectionTests.DecodeAnswersATruncatedBodyWithDecodeError also fails. TlsReader.Take (TlsReader.cs:126) advances position with no check against end, so ReadVector does not bound a vector by the bytes left. The Networking mutation baseline also had to leave out 3 HandBuiltTlsProviderTests ECH tests.
- 2026-10-18 | 2026-10-18_1905.md | not re-audited | Ran dotnet test Curl.Tls.UnitTests --filter "TestCategory!=Integration": 8 failures, then the run hung and was stopped. Every failing stack is topped by HandshakeMessageReader.Read line 32 (a separate defect, filed as quality:Curl.Tls.UnitLibrary/HandshakeMessageReader.cs:Read:failing-test), not TlsReader. TlsReader.ReadVector now calls Take(length), which checks end - position < count. The reproduction still fails for another cause, so it cannot tell whether AF-0231 is fixed.

## Log

- 2026-10-16: filed proposed.
- 2026-10-16: proposed -> accepted.
