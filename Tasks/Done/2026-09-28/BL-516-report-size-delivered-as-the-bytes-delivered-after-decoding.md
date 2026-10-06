---
id: BL-516
title: Report %{size_delivered} as the bytes delivered after decoding, apart from %{size_download}
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-516 — Report %{size_delivered} as the bytes delivered after decoding, apart from %{size_download}

## Goal

`%{size_delivered}` and `%{size_download}` print what curl 8.21.0 prints for each, which differ when the body is decoded (`--compressed`, chunked framing), instead of both coming from one source.

## Context

- Conformance audit 2026-09-28, row 43 (Major, "measure first"; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps both `size_download` and `size_delivered` to `DownloadSize`. `--compressed` decoding is in `Curl.Protocol.Http.UnitLibrary` (ADR-0020, ADR-0031, ADR-0070); what the progress reports count is ADR-0065.
- Do not assume which of the two is the wire count: measure it.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: a gzip body with `--compressed -w '%{size_download} %{size_delivered}'`, the same without `--compressed`, a chunked body, a plain body, and `-w '%{json}'`; stdout copied into Notes with the byte sizes of the canned responses.
- [x] The source of each variable is named in the XML docs, and `Curl.Output.UnitTests` pin both variables from distinct values.
- [x] `Curl.Console.UnitTests` (or `Curl.Protocol.Http.UnitTests`) pin each measured case end to end.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-28 against curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`,
`-s -o NUL -w '%{size_download} %{size_delivered} %{size_header}'` unless stated:

| Case | Canned body | stdout |
| --- | --- | --- |
| gzip, `--compressed` | 51-byte gzip (Content-Length: 51) of `Hello, compressed world! ` x20 + LF, 501 bytes | `51 501 89` |
| same gzip, no `--compressed` | as above | `51 51 89` |
| chunked (`5\r\nhello\r\n7\r\n world\n\r\n0\r\n\r\n`) | 12 bytes unframed, 29 on the wire | `12 12 47` |
| plain, Content-Length: 12 | `hello world\n` | `12 12 39` |
| gzip in two chunks (0x1a + 0x19), `--compressed`, `-w '%{size_download} %{size_delivered}'` | 51 unframed, 501 decoded | `51 501` |
| gzip, `--compressed -w '%{json}'` | as the first | `..."size_delivered":501,"size_download":51,"size_header":89,...` |
| plain to stdout, `-w '[%{size_download} %{size_delivered}]'` | `hello world\n` | `hello world\n[12 12]` |
| plain with `-i`, `-o NUL` | `hello world\n` | `12 12` (headers are not counted as delivered) |

So `size_download` is the body after chunked framing is removed and before content
decoding (as `TransferReport.DownloadSize` already said), and `size_delivered` is the body
after content decoding. No design choice was left open, so no ADR: the behaviour is the
measurement.

Implementation: `HttpContentDecoder.BytesDelivered` counts the decoded bytes the output
accepted; `HttpResponseBodyReader.BytesDelivered` is that, or `BytesWritten` when nothing is
decoded; `HttpExchange.Report` sets the new `TransferReport.DeliveredSize` (nullable, so
every other protocol's report keeps `size_delivered` equal to `size_download` without
change); `TransferWriteOutVariables` prints `DeliveredSize ?? DownloadSize`.

Not measured here and left as is: `--tr-encoding` with a gzip Transfer-Encoding, where
`BytesWritten` still counts the transfer-encoded bytes (BL-315); `size_delivered` now
counts the decoded bytes there too.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. %{size_delivered} prints the bytes after content decoding and %{size_download} the unframed body before it, as curl 8.21.0 measured (51 501 for a 51-byte gzip of 501)
