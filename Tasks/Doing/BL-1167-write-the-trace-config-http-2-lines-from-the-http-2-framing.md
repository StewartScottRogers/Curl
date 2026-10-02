---
id: BL-1167
title: Write the --trace-config http/2 lines from the HTTP/2 framing
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1167 — Write the --trace-config http/2 lines from the HTTP/2 framing

## Goal

Under `-v --trace-config http/2` (and `protocol`, `all`) Curl writes the `* [HTTP/2] ...` lines curl 8.21.0 writes for an HTTP/2 transfer, from its HTTP/2 code.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`http/2`, `protocol` or `all`) and hands the HTTP/2 code an `ITransferEvents` sink.
- Not measured yet. The Schannel build reaches HTTP/2 in clear text with `--http2-prior-knowledge` against an h2c server; HTTP/2 over TLS needs ALPN, so measure that on the OpenSSL build (Linux or macOS). `Record-CurlExchange.ps1 -Script` can serve the h2c frames; extend it if it falls short. curl writes `[HTTP/2] [<stream id>] ...` lines for frames sent and received (`[HTTP/2] [1] OPENED stream for ...`, `[HTTP/2] [1] [:method: GET]` and the like), which the measurement must confirm.

## Acceptance criteria

- [ ] The `[HTTP/2]` lines of an h2c prior-knowledge GET (Schannel) and an HTTPS ALPN GET (OpenSSL) under `-v --trace-config http/2` are measured and recorded in Notes.
- [ ] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `http/2` without `-v` write none.
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
