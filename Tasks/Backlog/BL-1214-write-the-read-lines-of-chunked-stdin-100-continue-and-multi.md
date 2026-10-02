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
completed:
---
# BL-1214 — Write the [READ] lines of chunked, stdin, 100-continue and multipart request bodies

## Goal

Under `--trace-config read` chunked, stdin (`-T -`), `100-continue` and `-F` multipart request bodies write curl 8.21.0's `[READ]` lines.

## Context

- Split from BL-1189 (ADR-0383): `HttpRequestBodyWriter` traces only unchunked `-d` and known-length `-T` HTTP/1.x bodies sent without waiting for `100 Continue`.
- Measured 2026-10-02 for `-T -` with `abc` on stdin: `add fread reader, len=-1 -> 0`, `client_read(len=65407) -> 0, nread=0, eos=0`, `client_read(len=65536) -> 0, nread=0, eos=0`, `Done waiting for 100-continue`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=3, eos=0`, `http_chunk, made chunk of 3 bytes -> 0`, `client_read(len=65536) -> 0, nread=8, eos=0`, `cr_in_read(len=65524, total=-1, read=3) -> 0, nread=0, eos=1`, `http_chunk, added last, empty chunk`, `client_read(len=65536) -> 0, nread=5, eos=1`. Multipart and HTTP/2 bodies not measured.

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` for `-T -`, `-T file` over 1 MiB (100-continue), `-H 'Transfer-Encoding: chunked' -d` and `-F`; stderr in Notes.
- [ ] Tests pin each case's `[READ]` lines and that none appears without `read` or `all`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-02: Created.
