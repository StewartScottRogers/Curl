---
id: BL-1214
title: Write the [READ] lines of chunked, stdin, 100-continue and multipart request bodies
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1214 — Write the [READ] lines of chunked, stdin, 100-continue and multipart request bodies

## Goal

Under `--trace-config read` chunked, stdin (`-T -`), `100-continue` and `-F` multipart request bodies write curl 8.21.0's `[READ]` lines.

## Context

- Split from BL-1189 (ADR-0383): `HttpRequestBodyWriter` traces only unchunked `-d` and known-length `-T` HTTP/1.x bodies sent without waiting for `100 Continue`.
- Measured 2026-10-02 for `-T -` with `abc` on stdin: `add fread reader, len=-1 -> 0`, `client_read(len=65407) -> 0, nread=0, eos=0`, `client_read(len=65536) -> 0, nread=0, eos=0`, `Done waiting for 100-continue`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=3, eos=0`, `http_chunk, made chunk of 3 bytes -> 0`, `client_read(len=65536) -> 0, nread=8, eos=0`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=0, eos=1`, `http_chunk, added last, empty chunk`, `client_read(len=65536) -> 0, nread=5, eos=1`. Multipart and HTTP/2 bodies not measured.

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` for `-T -`, `-T file` over 1 MiB (100-continue), `-H 'Transfer-Encoding: chunked' -d` and `-F`; stderr in Notes.
- [x] Tests pin each case's `[READ]` lines and that none appears without `read` or `all`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02 with `Record-CurlExchange.ps1` against curl 8.21.0 (Schannel), `-sv --trace-config read`, port 47811 (non-`[READ]` lines trimmed):

- `-T -` with `abc` on stdin: `add fread reader, len=-1 -> 0`, `client_read(len=65405) -> 0, nread=0, eos=0`, the head (131 bytes), `client_read(len=65536) -> 0, nread=0, eos=0`, `Done waiting for 100-continue`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=3, eos=0`, `http_chunk, made chunk of 3 bytes -> 0`, `client_read(len=65536) -> 0, nread=8, eos=0`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=0, eos=1`, `http_chunk, added last, empty chunk`, `client_read(len=65536) -> 0, nread=5, eos=1`, `upload completely sent off: 13 bytes`.
- `-H Expect: -T -` (no wait, 109-byte head): `add fread reader, len=-1 -> 0`, `cr_in_read(len=65415, total=-1, read=3) -> 0, nread=3, eos=0`, `http_chunk, made chunk of 3 bytes -> 0`, `client_read(len=65427) -> 0, nread=8, eos=0`, the head, then the same three end-of-body lines as above.
- `-T big.bin` (1100000 bytes, 128-byte head), with the wait timing out and with it answered by `100 Continue` (`-Script`): identical but for `Done waiting for 100-continue`, which only the timed-out run writes: `add fread reader, len=1100000 -> 0`, `client_read(len=65408) -> 0, nread=0, eos=0`, the head, `client_read(len=65536) -> 0, nread=0, eos=0`, then 16 pairs `cr_in_read(len=65536, total=1100000, read=65536*k) -> 0, nread=65536, eos=0` / `client_read(len=65536) -> 0, nread=65536, eos=0`, then `cr_in_read(len=51424, total=1100000, read=1100000) -> 0, nread=51424, eos=1`, `client_read(len=65536) -> 0, nread=51424, eos=1`.
- `-H 'Transfer-Encoding: chunked' -d ab` (157-byte head): `add buf reader, len=2 -> 0`, `cr_buf_read(len=65367) -> 0, nread=2, eos=1`, `http_chunk, made chunk of 2 bytes -> 0`, `http_chunk, added last, empty chunk`, `client_read(len=65379) -> 0, nread=12, eos=1`, then the head.
- `-F a=b` (149-byte body, 193-byte head): `cr_mime_read(len=149), mime_read() -> 149`, `cr_mime_read(len=149, total=149, read=149) -> 0, 149, 1`, `client_read(len=65343) -> 0, nread=149, eos=1`, then the head. No `add ... reader` line.
- `-F f=@mid.bin` (70000-byte file, 70208-byte body, 195-byte head): `cr_mime_read(len=65341), mime_read() -> 65341`, `cr_mime_read(len=65341, total=70208, read=65341) -> 0, 65341, 0`, `client_read(len=65341) -> 0, nread=65341, eos=0`, the head, `cr_mime_read(len=4867), mime_read() -> 4867`, `cr_mime_read(len=4867, total=70208, read=70208) -> 0, 4867, 1`, `client_read(len=65536) -> 0, nread=4867, eos=1`.

Plan and choices (ADR-0389): `HttpRequestBodyWriter` now traces every non-empty HTTP/1.x body; the held-back reads around a waiting head come from the new `WriteHeadBeforeContinueAsync`, which the handler calls in place of `WriteHeldHeadAsync` before the wait. A wait ended by a final status (417, 3xx) writes the reader and the held-back reads only: not measured, the simplest consistent choice. HTTP/2 and HTTP/3 bodies still write none (unmeasured). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line and branch; its two complexity flags (`HttpProtocolHandler.ExchangeAsync`, `HttpResponseHeadReader..ctor`, both 12) are in members this task did not change, and the build-time CA1502 gate passes.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Chunked, stdin, 100-continue and multipart HTTP/1.x bodies write curl's [READ] trace lines
