---
id: BL-5359
title: Fix AF-0839: ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody checks only that some body exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5359 — Fix AF-0839: ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody checks only that some body exists

## Goal

The defect the audit office reported as AF-0839 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0839 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:77`

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:77`

The only assertion is Assert.IsNotNull(http2.Body);. The one-byte body the name says is kept is never compared.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs -SimpleMatch -Pattern 'Assert.IsNotNull(http2.Body);'
```

- Expected: No match.
- Actual: Matches at lines 68 and 77.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Line 77 was already fixed by 56e5de8c; this run replaced the remaining IsNotNull at line 58 with AreSame. Reproduction now gives no match; build clean, Http.UnitTests fast tests 2702 passed.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-17: Doing -> Done. Last IsNotNull(http2.Body) replaced by AreSame; reproduction gives no match
