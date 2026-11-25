---
id: AF-0839
title: ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody checks only that some body exists
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody:weak-assertion
reproduction: none
task: BL-5359
tasks: BL-5359
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
---
# AF-0839 - ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody checks only that some body exists

## Summary

Low finding from the quality auditor at `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:77`: ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody checks only that some body exists.

## Evidence

Location: `Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs:77`

The only assertion is Assert.IsNotNull(http2.Body);. The one-byte body the name says is kept is never compared.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Http2RequestHeadersTransferEncodingTests.cs -SimpleMatch -Pattern 'Assert.IsNotNull(http2.Body);'
```

- Expected: No match.
- Actual: Matches at lines 68 and 77.

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the Select-String: no match. ForHttp2OrHttp3_KnownBodyWithChunkedHeader_KeepsItsBody now asserts line 77 Assert.AreSame(options.Body, http2.Body);.
- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Select-String for 'Assert.IsNotNull(http2.Body);' in Http2RequestHeadersTransferEncodingTests.cs finds no match.

## Log

- 2026-11-17: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
