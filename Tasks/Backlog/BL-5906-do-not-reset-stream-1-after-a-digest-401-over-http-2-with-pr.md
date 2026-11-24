---
id: BL-5906
title: Do not reset stream 1 after a Digest 401 over HTTP/2 with prior knowledge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests/Http2Session]
requirement: none
created: 2026-11-24
completed:
---
# BL-5906 — Do not reset stream 1 after a Digest 401 over HTTP/2 with prior knowledge

## Goal

`curl --http2-prior-knowledge --digest -u tester:secret <url>` sends no `RST_STREAM` for stream 1 between the 401 and the second `HEADERS` on stream 3, as upstream curl 8.21.0 does.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 25 (Surl `Http2.md` finding 2), seen on osx-arm64 and linux-x64 in Surl's `curl-port-phase-7` run 45412760425 (2026-11-24) at port commit `74f3f45` (2026-11-16). Check it still reproduces on the current branch first.

Command: `curl -sS -f --http2-prior-knowledge --digest -u tester:secret -w %{http_version} <surl>/hello.txt` against `surl --user-file <tester:secret>`. Both exit 0 with the same body and version. Upstream, after the 401, sends the `SETTINGS` ack and a second `HEADERS` on stream 3 with the Digest `Authorization`, and no reset. The port also sends `RST_STREAM stream=1 error=0x5 (STREAM_CLOSED)` between them. Stream 1 is already closed by the server's END_STREAM, so RFC 9113 section 5.4 gives no reason to reset it. Likely the port resets a stream it abandons on an auth retry even when it already ended. Start at `Http2Session.cs` / `Http2StreamConnection.cs` (the RST_STREAM writers). Check that a 401 whose body was not fully read still resets with CANCEL as upstream does, if upstream does.

## Acceptance criteria

- [ ] After a Digest 401 whose stream ended (END_STREAM received), the port writes no `RST_STREAM` for it before the retry, pinned by a unit test on the frames written to a test-double connection.
- [ ] Platform-neutral: the test passes on Windows, Linux and macOS.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 25.
