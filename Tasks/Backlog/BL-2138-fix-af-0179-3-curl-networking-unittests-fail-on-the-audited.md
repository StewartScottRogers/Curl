---
id: BL-2138
title: Fix AF-0179: 3 Curl.Networking.UnitTests fail on the audited commit: TlsReader.Take has no bounds check, so a truncated ECHConfigList throws IndexOutOfRangeException
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2138 — Fix AF-0179: 3 Curl.Networking.UnitTests fail on the audited commit: TlsReader.Take has no bounds check, so a truncated ECHConfigList throws IndexOutOfRangeException

## Goal

The defect the audit office reported as AF-0179 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0179 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0179-3-curl-networking-unittests-fail-on-the-audited-co.md`.

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

Invoke-MutationTest.ps1's unmutated baseline for Curl.Networking.UnitLibrary left out 3 failing tests: HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. A filtered dotnet test run gave 'Failed: 5, Passed: 42' (5 data rows). Every failing row has a DNS lookup returning a malformed list ([0x00,0x05,0xfe] or [0x00,0x04,0xfe,0x0d,0x00,0x00]), and the error is 'System.IndexOutOfRangeException: Index was outside the bounds of the array. at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:119) <- ReadUInt16 <- EchConfigList.ReadConfigs (EchConfigList.cs:42) <- EchConfigList.Decode <- EchOffer.Decoded <- EchOffer.DecideAsync <- HandBuiltTlsProvider.PrepareWithEchAsync'. TlsReader.Take(int count) checks only 'failure.Alert is not null' and then does 'position += count; return true;'. It never checks count against 'end - position', so a length prefix past the end of a vector reads outside it and indexes past the buffer instead of recording TlsAlertDescription.DecodeError. A malformed peer- or DNS-supplied structure therefore crashes the transfer with an unhandled exception instead of curl's behaviour: a plain hello for --ech true, or exit 35 for --ech hard. The tests catch it: they are red on the audited commit.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests --filter "FullyQualifiedName~HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch"
```

- Expected: All 47 tests pass; a truncated ECHConfigList decodes as DecodeError.
- Actual: Failed!  - Failed: 5, Passed: 42, Total: 47; System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:119).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
