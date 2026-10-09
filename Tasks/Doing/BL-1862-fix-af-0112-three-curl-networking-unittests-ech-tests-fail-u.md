---
id: BL-1862
title: Fix AF-0112: Three Curl.Networking.UnitTests ECH tests fail unmutated: TlsReader.Take advances past the end with no bounds check
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1862 — Fix AF-0112: Three Curl.Networking.UnitTests ECH tests fail unmutated: TlsReader.Take advances past the end with no bounds check

## Goal

The defect the audit office reported as AF-0112 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0112 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0112-three-curl-networking-unittests-ech-tests-fail-unm.md`.

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

Location: `Curl.Tls.UnitLibrary/TlsReader.cs:126`

The Networking mutation baseline left out 3 tests that fail unmutated: HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. Rerun on the audited tree, each throws System.IndexOutOfRangeException at TlsReader.ReadUnsigned (TlsReader.cs:113) <- ReadUInt16 <- EchConfigList.ReadConfigs (EchConfigList.cs:42) <- EchOffer.Decoded. TlsReader.Take (line 126) only checks for an earlier failure and then does 'position += count; return true;', with no check that position + count stays within the end, so a truncated vector reads past the buffer instead of recording DecodeError. Curl.Tls.UnitTests also fails on the audited tree (e.g. 'A list length cut short', 'ATruncatedBodyIsADecodeError', DecodeAnswersATruncatedBodyWithDecodeError for every handshake type). A malformed ECH config list or TLS message from a peer crashes the transfer instead of failing with curl's exit code.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Networking.UnitTests -c Release -nologo --filter "Name=AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello|Name=AuthenticateAsClientAsync_WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte|Name=AuthenticateAsClientAsync_WithEchTrueAndNoUsableList_SendsAPlainHello"
```

- Expected: Passed! - Failed: 0.
- Actual: Failed! - Failed: 5, Passed: 32, Total: 37; every failure is System.IndexOutOfRangeException at Curl.Tls.TlsReader.ReadUnsigned (TlsReader.cs:113).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
