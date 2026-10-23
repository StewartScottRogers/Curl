---
id: BL-3209
title: Record --http2 (h2c and TLS), --http2-prior-knowledge and ALPN fallback against an nghttp2-built curl 8.22.0 with an HTTP/2 origin in Record-CurlExchange.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1, Documentation/Product/Http2OriginRecordings.md]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3209 — Record --http2 (h2c and TLS), --http2-prior-knowledge and ALPN fallback against an nghttp2-built curl 8.22.0 with an HTTP/2 origin in Record-CurlExchange.ps1

## Goal

`Record-CurlExchange.ps1` can serve an HTTP/2 origin, and recordings of an nghttp2-built curl 8.22.0 pin what BL-3207 compares Curl against: request bytes, `-w %{http_version}`, `-v` lines and exit codes for `--http2` over h2c and TLS, `--http2-prior-knowledge`, and ALPN fallback to HTTP/1.1.

## Context

BL-3207 turns HTTP/2 on in the Windows CLI (`--http2`, `--http2-prior-knowledge`, `--proxy-http2`) and its acceptance criteria need a comparison against an upstream curl 8.22.0 built with nghttp2. BL-3207's first pass (2026-10-22, lane 7) found two things missing:
- The recorder has only an HTTP/2 *proxy* (`-Http2`, class `RecorderHttp2Proxy`, around line 2151); it has no HTTP/2 origin. Add one: answer `Upgrade: h2c` with `101 Switching Protocols` then frames (or ignore the upgrade, for the "h2c upgrade refusal" case), accept the connection preface on plain TCP (prior knowledge), and pick `h2` or `http/1.1` by ALPN over TLS (fallback). Log the preface and every frame curl sends, as `RecorderHttp2Proxy.ServeHttp2` does.
- No nghttp2-built curl 8.22.0 is on this machine: `/ucrt64/bin/curl` is 8.22.0 Schannel without nghttp2, WinGet's curl and WSL Ubuntu's curl are 8.18.0 with nghttp2/1.68.0. curl.se/windows publishes curl-for-win 8.22.0 with nghttp2; the gap-platforms.yml OpenSSL build is another source. Download into its own empty directory and pass it with `-Curl`.

Write the recorded results (command lines, request bytes as hex or escaped text, `%{http_version}`, the `-v` lines that differ by version, exit codes) to `Documentation/Product/Http2OriginRecordings.md` so BL-3207 can pin Curl against them.

## Acceptance criteria

- [x] `Record-CurlExchange.ps1` has a documented HTTP/2 origin mode covering h2c upgrade (accepted and ignored), prior knowledge, and TLS with ALPN `h2` or `http/1.1`.
- [x] `Documentation/Product/Http2OriginRecordings.md` records an nghttp2-built curl 8.22.0's request bytes, `%{http_version}`, `-v` lines and exit codes for `--http2` over h2c, `--http2` over TLS, `--http2-prior-knowledge`, and ALPN fallback to HTTP/1.1, naming the curl build used.

## Notes

- Added `-Http2Origin` (h2c-upgrade, h2c-upgrade-ignored, prior-knowledge, alpn) to the existing RecorderHttp2Proxy class. Recorded with curl-for-win 8.22.0_1 (LibreSSL, nghttp2/1.70.0). Only a script and a document changed, so no C# build or test applies.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a252bd00 6db5a716.
- 2026-10-22: Doing -> Done. Record-CurlExchange.ps1 -Http2Origin serves h2c/prior-knowledge/ALPN origins; nghttp2 curl 8.22.0 recorded in Http2OriginRecordings.md
