---
id: BL-4461
title: Fix AF-0525: UrlRejectionText's summary names only the IDNA failure as printed without 'URL rejected: ', but UrlTooLong and UrlAtLengthLimit are too
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-02
completed:
---
# BL-4461 — Fix AF-0525: UrlRejectionText's summary names only the IDNA failure as printed without 'URL rejected: ', but UrlTooLong and UrlAtLengthLimit are too

## Goal

The defect the audit office reported as AF-0525 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0525 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0525-urlrejectiontext-s-summary-names-only-the-idna-fai.md`.

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:32`

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:32`

The summary (lines 30-34) says the text is 'UrlRejectedPrefix and the parser's reason, except for a host IDNA cannot convert, which curl 8.22.0 reports with CURLE_URL_MALFORMAT's own text'. The method (line 38) returns the bare text whenever IsBareText(rejection) is true, and IsBareText (line 42) is 'rejection is CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong or CurlUrlRejection.UrlAtLengthLimit'. So the doc says UrlTooLong and UrlAtLengthLimit print 'URL rejected: A libcurl function was given a bad argument' and 'URL rejected: Out of memory', but the code prints the bare text for both (with exits 43 and 27, lines 47-48). This is the omission AF-0523 reported in CurlUrlRejectionMessages, which has since been fixed there; the engine's own comment still has it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs -Pattern 'except for a host IDNA cannot convert|CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong'
```

- Expected: The summary names all three bare-text rejections (IdnConversionFailed, UrlTooLong, UrlAtLengthLimit), so the first pattern does not match while the second does.
- Actual: Both match: line 32 '/// parser's reason, except for a host IDNA cannot convert, which curl 8.22.0 reports with' and line 42 'rejection is CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong or CurlUrlRejection.UrlAtLengthLimit;'.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The summary of `UrlRejectionText` now names all three bare-text rejections (IdnConversionFailed, UrlTooLong, UrlAtLengthLimit) with each code's text. Comment-only change, so no member's metrics changed; built Curl.Engine.UnitTests and ran its fast tests (2945 passed). The reproduction now matches only line 45.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
