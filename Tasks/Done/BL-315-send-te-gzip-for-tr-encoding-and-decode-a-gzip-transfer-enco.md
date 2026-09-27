---
id: BL-315
title: Send TE: gzip for --tr-encoding and decode a gzip Transfer-Encoding in the HTTP handler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-180]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-315 — Send TE: gzip for --tr-encoding and decode a gzip Transfer-Encoding in the HTTP handler

## Goal

With `--tr-encoding`, the HTTP handler sends `TE: gzip` and `Connection: TE` and decodes a response whose Transfer-Encoding lists `gzip` (and `deflate`), as curl 8.21.0 does.

## Context

- Split out of BL-180 (2026-09-26): `HttpRequestOptions` has no member for `--tr-encoding`, so the handler cannot see it. Add one (e.g. `bool TransferEncoding`) to `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs`; `CommandLineOptions` already parses the option (BL-191). BL-236 wires it in Curl.Console.
- Measured on curl 8.21.0 (mingw, `/mingw64/bin/curl`) with `Record-CurlExchange.ps1 -Port 18180 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs '--tr-encoding','http://127.0.0.1:18180/a'`: the request was `GET /a HTTP/1.1`, `Host: 127.0.0.1:18180`, `User-Agent: curl/8.21.0`, `Accept: */*`, `TE: gzip`, `Connection: TE`, then the empty line; stdout `hello`, exit 0.
- Without `--tr-encoding`, `Transfer-Encoding: gzip` is exit 61 (`HttpTransferEncoding`, BL-171). Still to measure: a gzip and a `gzip, chunked` Transfer-Encoding with `--tr-encoding`, `-H "Connection: x"` alongside it, and `--raw --tr-encoding`.
- Upstream: https://curl.se/docs/manpage.html#--tr-encoding

## Acceptance criteria

- [x] `HttpRequestOptions` carries `--tr-encoding`, and a `Curl.Protocol.Abstractions.UnitTests` test pins its default (off).
- [x] The request bytes above are pinned in a `Curl.Protocol.Http.UnitTests` test, and every `--tr-encoding` response case listed in Context is measured on curl 8.21.0, recorded in Notes and pinned, with 1-byte reads too.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http` and `Curl.Protocol.Abstractions`.

## Notes

### Plan (2026-09-27)

`HttpRequestOptions.TransferEncoding` (bool). `HttpRequestHeadFormatter` sends `TE: gzip` after
`Accept` and a `Connection` line last. `HttpTransferEncoding.Requested` reads the
Transfer-Encoding headers the `--tr-encoding` way; `HttpResponseBodyFraming.Of(..., decodesTransferCoding)`
frames the body from it and carries the codings; `HttpContentDecoder.ForCodings` decodes
Transfer-Encoding codings with the same per-coding decoders as Content-Encoding, transfer codings
first. `HttpResponseBodyReader.DecodesTransferCoding` and a `decodeTransfer` argument on
`CopyAsync` (false for a discarded body); `HttpConnectionPersistence.KeepsAlive` takes the flag too.

### Measured on curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Port 18180`, `-sS --tr-encoding` unless stated

`{gz}` is the 25-byte gzip of `hello`; every response closed after sending.

Request:
- Plain: `GET /a HTTP/1.1`, Host, User-Agent, Accept, `TE: gzip`, `Connection: TE`.
- `--compressed` too: `TE: gzip` comes before `Accept-Encoding`; `Connection: TE` last.
- All together (`-u u:p --compressed -e http://r/ -H "X-A: 1" -H "Connection: x" -b c=1 -z <date> -d data`): Host, Authorization, User-Agent, Accept, TE, Accept-Encoding, Referer, Cookie, If-Modified-Since, X-A, Content-Length, Content-Type, `Connection: x, TE`.
- With a >1 MiB body, `Connection: a, TE` comes after `Expect: 100-continue`.
- `-H "Connection: x"` → `Connection: x, TE` (last); `Connection: close` → `close, TE`; `Connection:`, `Connection;` → `Connection: TE`; `Connection:   x  ` → `x, TE`; `connection: x` → `Connection: x, TE`.
- `-H "Connection: a" -H "X-B: 2" -H "Connection: b"` → `X-B: 2`, `Connection: a, TE`, `Connection: b`. `Connection;` or `Connection:` then `Connection: b` → `Connection: b, TE`.
- `-H "TE: x"` → `TE: x` and no Connection; `TE:` → neither; `TE;` → `TE:` and no Connection; `TE: x` + `Connection: y` → both as given, Connection last.
- `Connection :x`, `Connectionx: a`, `TEx: a` name nothing: sent in place, `TE: gzip` and `Connection: TE` still sent.
- `-0` → `GET /a HTTP/1.0` with TE and Connection.
- Through `-x`: `TE: gzip` then `Proxy-Connection: Keep-Alive`, customs, `Connection: TE`. A `--proxy-header "Connection: p"` is never sent (with or without `--tr-encoding`); `--proxy-header "TE: p"` is sent and does not stop `TE: gzip`.
- Without `--tr-encoding`: custom `Connection` lines still go last, after the body headers (`X-B: 2`, Content-Length, Content-Type, `Connection: a`, `Connection: b`), and `Connection;` or a blank `Connection:   ` is not sent at all. Curl sent them in command-line order before; fixed here because it is the same rule.

Response, exit 0 with stdout `hello`: `gzip` with Content-Length 25; `gzip, chunked`; `deflate` (zlib); `x-gzip`; `br`; `identity`; `, GZip ,`; `gzip` to close; `gzip` with Content-Length 12 (Content-Length not trusted: body runs to close); `Transfer-Encoding: gzip` then a `Transfer-Encoding: chunked` header; Content-Encoding gzip + Transfer-Encoding gzip without `--compressed` (only the transfer coding decoded); five `identity`; four `identity` + `chunked`; `HTTP/1.0` gzip to close. Content-Encoding gzip + Transfer-Encoding gzip with `--compressed` and a doubly gzipped body → `hello`. Empty body with `gzip` or `foo` → exit 0, nothing written; `-I` with `foo` → exit 0.

Response, exit 61:
- `gzip` over `hello`, and `gzip, gzip` over one gzip stream → `Error while processing content unencoding: incorrect header check`.
- `foo`, and `foo, chunked` → `Unrecognized content encoding type` (at the first body byte).
- `chunked, gzip`, `chunked, identity`, and a `chunked` header then a `gzip` header → `Reject response due to 'chunked' not being the last Transfer-Encoding`.
- Six `identity`; five `identity` + `chunked`; three + three in two headers → `Reject response exceeding limit of 5 transfer encodings`.
- `zstd` over `hello` → `Unrecognized or bad HTTP Content or Transfer-Encoding` (curl decodes zstd; Curl does not, ADR-0020, so Curl says `Unrecognized content encoding type` - not pinned).
- Without `--tr-encoding`, `gzip` is still `Unsolicited Transfer-Encoding (gzip) found` (BL-171).

`--raw --tr-encoding`: `gzip` Content-Length 25 → `hello` (still decoded); `chunked` → the chunk lines raw; `gzip, chunked` → exit 61 incorrect header check (gzip decoded over the raw chunk bytes); `foo` → exit 61 `Unrecognized content encoding type`; `--raw --compressed` with Content-Encoding gzip → the gzip bytes undecoded.

Other: 302 with `Transfer-Encoding: foo` under `-L --max-redirs 0` → exit 47, not 61 (the discarded body is not decoded); without `-L` exit 61. `-f` on a 404 with `foo` → exit 22.

### Decisions (defaults taken, unattended run)

- A discarded body (3xx under `-L`, a 401 retried) is framed the `--tr-encoding` way but not decoded, like `--compressed` (BL-177): matches the measured exit 47.
- Under `--raw`, `chunked` is neither counted towards the limit of five nor checked for being last, since curl does not add a chunked decoder then. Not measured; the simplest reading of curl's code.
- A Content-Length header before the first Transfer-Encoding header is still checked (exit 8 when invalid); one after it is not read, as curl sets its ignore-Content-Length flag on the Transfer-Encoding header.
- No ADR: every behaviour here was measured except the `--raw` counting default above, and `zstd` is already ADR-0020.

### Follow-ups filed

- BL-359: the same limit of five for Content-Encoding (`Reject response exceeding limit of 5 content encodings`, measured), which Curl does not enforce.
- BL-360: bytes after the end of a gzip stream (`hello` + exit 23 `Failed writing received data to disk/application`, measured under `--tr-encoding`).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --tr-encoding sends TE: gzip and Connection: TE and decodes gzip, deflate and br Transfer-Encodings as curl 8.21.0 does
