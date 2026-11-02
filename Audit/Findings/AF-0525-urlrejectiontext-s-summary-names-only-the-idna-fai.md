---
id: AF-0525
title: UrlRejectionText's summary names only the IDNA failure as printed without 'URL rejected: ', but UrlTooLong and UrlAtLengthLimit are too
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:UrlRejectionText:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-11-01
found-at: 379ec9aa19ade61c2ce4cb9ee9b729c5aa926fc5
scorecard: 2026-11-01_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0525 - UrlRejectionText's summary names only the IDNA failure as printed without 'URL rejected: ', but UrlTooLong and UrlAtLengthLimit are too

## Summary

Medium finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:32`: UrlRejectionText's summary names only the IDNA failure as printed without 'URL rejected: ', but UrlTooLong and UrlAtLengthLimit are too. Reported by an auditor flagged unreliable in 2026-11-01_1801.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:32`

The summary (lines 30-34) says the text is 'UrlRejectedPrefix and the parser's reason, except for a host IDNA cannot convert, which curl 8.22.0 reports with CURLE_URL_MALFORMAT's own text'. The method (line 38) returns the bare text whenever IsBareText(rejection) is true, and IsBareText (line 42) is 'rejection is CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong or CurlUrlRejection.UrlAtLengthLimit'. So the doc says UrlTooLong and UrlAtLengthLimit print 'URL rejected: A libcurl function was given a bad argument' and 'URL rejected: Out of memory', but the code prints the bare text for both (with exits 43 and 27, lines 47-48). This is the omission AF-0523 reported in CurlUrlRejectionMessages, which has since been fixed there; the engine's own comment still has it.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs -Pattern 'except for a host IDNA cannot convert|CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong'
```

- Expected: The summary names all three bare-text rejections (IdnConversionFailed, UrlTooLong, UrlAtLengthLimit), so the first pattern does not match while the second does.
- Actual: Both match: line 32 '/// parser's reason, except for a host IDNA cannot convert, which curl 8.22.0 reports with' and line 42 'rejection is CurlUrlRejection.IdnConversionFailed or CurlUrlRejection.UrlTooLong or CurlUrlRejection.UrlAtLengthLimit;'.

## Re-audits

## Log

- 2026-11-01: filed proposed.
