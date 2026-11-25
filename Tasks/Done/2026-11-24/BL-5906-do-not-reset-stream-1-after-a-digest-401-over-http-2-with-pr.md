---
id: BL-5906
title: Do not reset stream 1 after a Digest 401 over HTTP/2 with prior knowledge
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Http.UnitTests/Http2Session, Curl.Original.Protocol.Http.UnitTests/Http2StreamConnectionTests.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http2.cs, Documentation/Planning/Decisions/ADR-0343-an-ignored-http2-body-resets-its-stream.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5906 — Do not reset stream 1 after a Digest 401 over HTTP/2 with prior knowledge

## Goal

`curl --http2-prior-knowledge --digest -u tester:secret <url>` sends no `RST_STREAM` for stream 1 between the 401 and the second `HEADERS` on stream 3, as upstream curl 8.21.0 does.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 25 (Surl `Http2.md` finding 2), seen on osx-arm64 and linux-x64 in Surl's `curl-port-phase-7` run 45412760425 (2026-11-24) at port commit `74f3f45` (2026-11-16). Check it still reproduces on the current branch first.

Command: `curl -sS -f --http2-prior-knowledge --digest -u tester:secret -w %{http_version} <surl>/hello.txt` against `surl --user-file <tester:secret>`. Both exit 0 with the same body and version. Upstream, after the 401, sends the `SETTINGS` ack and a second `HEADERS` on stream 3 with the Digest `Authorization`, and no reset. The port also sends `RST_STREAM stream=1 error=0x5 (STREAM_CLOSED)` between them. Stream 1 is already closed by the server's END_STREAM, so RFC 9113 section 5.4 gives no reason to reset it. Likely the port resets a stream it abandons on an auth retry even when it already ended. Start at `Http2Session.cs` / `Http2StreamConnection.cs` (the RST_STREAM writers). Check that a 401 whose body was not fully read still resets with CANCEL as upstream does, if upstream does.

## Acceptance criteria

- [x] After a Digest 401 whose stream ended (END_STREAM received), the port writes no `RST_STREAM` for it before the retry, pinned by a unit test on the frames written to a test-double connection.
- [x] Platform-neutral: the test passes on Windows, Linux and macOS.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced on the branch by reading the code: `AbandonResponseAsync` reset any stream whose
  END_STREAM it had not yet taken, though the DATA carrying it was still unread.
- Measured curl 8.18.0 (nghttp2) with `Record-CurlExchange.ps1 -Http2Origin ... -Http2Frames`:
  - prior knowledge, Digest 401 or 302 `-L`, DATA END_STREAM: no RST_STREAM; HEADERS on stream 3.
  - prior knowledge, 401 or 302 never ended: curl waits for the body and does not retry;
    RST_STREAM STREAM_CLOSED (not CANCEL) only when `-m 3` gives up. So the Context's
    "still resets with CANCEL" does not happen upstream, and Curl now waits too.
  - h2c upgrade, Digest 401, DATA END_STREAM: RST_STREAM STREAM_CLOSED (as BL-970): kept.
- Fix (ADR-1357): `AbandonResponseAsync` reads a stream whose request has ended to its end;
  an upgraded stream, or one whose request has not ended (unmeasured), is still reset.
- The built Curl against the first case sends the same frame types and streams as curl, except
  curl's RST_STREAM on the unanswered stream 3 at the timeout: filed as BL-5922.
- `touches` widened (no other task in Doing names the HTTP library): `HttpProtocolHandler.cs`
  (comment only), the library's `CLAUDE.md`, the two test files (the `Http2Session` test folder
  does not exist), and ADR-0343's status line.
- Platform-neutral: the tests are in memory over `ScriptedConnection`; no paths or OS text.
- `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 25.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ef2a122e eb0dd7e6 3d836d5e.
- 2026-11-24: Doing -> Done. A discarded HTTP/2 body on a sent stream is read to its end with no RST_STREAM, as curl does; upgraded streams still reset.
