---
id: BL-5358
title: Fix AF-0838: ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody checks only that some body exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5358 — Fix AF-0838: ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody checks only that some body exists

## Goal

The defect the audit office reported as AF-0838 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0838 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:68`

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:68`

The only assertion is Assert.IsNotNull(http2.Body);. The name promises its own body is kept, but any replacement body passes; nothing compares http2.Body with options.Body.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs -SimpleMatch -Pattern 'Assert.IsNotNull(http2.Body);'
```

- Expected: No match: the test asserts AreSame(options.Body, http2.Body).
- Actual: Matches at lines 68 and 77.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
