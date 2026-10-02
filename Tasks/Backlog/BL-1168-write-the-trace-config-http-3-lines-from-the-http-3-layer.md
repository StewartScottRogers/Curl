---
id: BL-1168
title: Write the --trace-config http/3 lines from the HTTP/3 layer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1168 — Write the --trace-config http/3 lines from the HTTP/3 layer

## Goal

Under `-v --trace-config http/3` (and `protocol`, `all`) Curl writes the `* [HTTP/3] ...` lines curl 8.21.0 writes for an HTTP/3 transfer, from `Curl.Http3.UnitLibrary`.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`http/3`, `protocol` or `all`) and hands the HTTP/3 layer an `ITransferEvents` sink.
- Not measured yet: the reference Schannel build has no HTTP/3. Measure on an OpenSSL build of curl 8.21.0 with HTTP/3 (ngtcp2/nghttp3) against a local HTTP/3 server, run with `Record-CurlExchange.ps1 -NoServer`, and record the lines in Notes.

## Acceptance criteria

- [ ] The `[HTTP/3]` lines of an HTTP/3 GET under `-v --trace-config http/3` are measured and recorded in Notes.
- [ ] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `http/3` without `-v` write none.
- [ ] `--ai-help` still describes `--trace-config` correctly.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
