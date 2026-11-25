---
id: AF-0838
title: ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody checks only that some body exists
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody:weak-assertion
reproduction: none
task: BL-5358
tasks: BL-5358
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
---
# AF-0838 - ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody checks only that some body exists

## Summary

Low finding from the quality auditor at `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:68`: ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody checks only that some body exists.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:68`

The only assertion is Assert.IsNotNull(http2.Body);. The name promises its own body is kept, but any replacement body passes; nothing compares http2.Body with options.Body.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs -SimpleMatch -Pattern 'Assert.IsNotNull(http2.Body);'
```

- Expected: No match: the test asserts AreSame(options.Body, http2.Body).
- Actual: Matches at lines 68 and 77.

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the Select-String: no match for 'Assert.IsNotNull(http2.Body);'. ForHttp2OrHttp3_EmptyBodyWithChunkedHeader_KeepsItsBody now asserts line 68 Assert.AreSame(options.Body, http2.Body);.
- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Select-String for 'Assert.IsNotNull(http2.Body);' in Http2RequestHeadersTransferEncodingTests.cs finds no match.

## Log

- 2026-11-17: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
