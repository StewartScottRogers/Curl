---
id: BL-1205
title: Write the --trace-config http/2 lines of an h2c upgrade and the status and header echoes
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1167]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1205 — Write the --trace-config http/2 lines of an h2c upgrade and the status and header echoes

## Goal

Under `-v --trace-config http/2` an h2c-upgraded transfer writes the `[HTTP/2]` frame lines too, and every HTTP/2 response writes curl's `[HTTP/2] [1] status: HTTP/2 200` and `[HTTP/2] [1] header: name: value` echo after each `<` line, as curl 8.21.0 does.

## Context

- Follow-up of BL-1167 (ADR-0373). `Http2FrameTrace` writes the frame lines for prior-knowledge and ALPN streams; `HttpH2cUpgradeConnection` builds its `Http2StreamConnection` without a trace, so an h2c upgrade writes none.
- The `status:` / `header:` echoes (measured in BL-1167's Notes) interleave with the `<` header lines the handler writes, so they belong where the response head is written, not in the frame layer.
- Measure the h2c upgrade's lines with an OpenSSL build (WSL); the Schannel build has no HTTP/2.

## Acceptance criteria

- [ ] An h2c upgrade under `-v --trace-config http/2` writes the measured `[HTTP/2]` lines; tests pin them.
- [ ] The `status:` and `header:` echoes are measured and pinned, after each `<` line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
