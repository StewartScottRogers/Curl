---
id: BL-4194
title: Move the HTTP/3 client session from Curl.Protocol.Http into Curl.Http3.UnitLibrary so extensions can open request streams
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-4201]
touches: [Curl.Protocol.Http.UnitLibrary/Http3Session.cs, Curl.Protocol.Http.UnitLibrary/Http3StreamConnection.cs, Curl.Protocol.Http.UnitTests/Http3SessionTests.cs, Curl.Protocol.Http.UnitTests/Http3StreamConnectionTests.cs, Curl.Http3.UnitLibrary/Http3ClientSession.cs, Curl.Http3.UnitLibrary/Http3ClientStreamConnection.cs, Curl.Http3.UnitTests/Http3ClientSessionTests.cs, Curl.Http3.UnitTests/Http3ClientStreamConnectionTests.cs, Curl.Http3.UnitLibrary/Curl.Http3.UnitLibrary.csproj, Curl.Http3.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4194 — Move the HTTP/3 client session from Curl.Protocol.Http into Curl.Http3.UnitLibrary so extensions can open request streams

## Goal

The HTTP/3 client session (QUIC connection, control and QPACK streams, request streams) lives in `Curl.Http3.UnitLibrary`, so a caller outside `Curl.Protocol.Http` can open a request stream with the pseudo-headers it chooses (such as `:protocol` for RFC 9220 extended CONNECT) and read the peer's SETTINGS, with no change to what curl does.

## Context

Found by BL-4112's first pass: `Http3Session.cs` and `Http3StreamConnection.cs` are in `Curl.Protocol.Http.UnitLibrary`, which an extension may not reference (ADR-0999). `Curl.Http2.UnitLibrary` already holds `Http2Connection`, so HTTP/2 needs no such move. Move the tests with the code; keep the names that say what they do (`Http3ClientSession` is a suggestion).

## Acceptance criteria

- [x] The HTTP/3 client session and stream connection are public types in `Curl.Http3.UnitLibrary`, with their unit tests in `Curl.Http3.UnitTests`, and `Curl.Protocol.Http.UnitLibrary` uses them.
- [x] A caller can send a request stream's HEADERS with any pseudo-header list and read whether the peer's SETTINGS carried `SETTINGS_ENABLE_CONNECT_PROTOCOL` (0x08); unit tests.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-29 first pass (lane 2): a straight move is impossible. `Http3Session.cs` and `Http3StreamConnection.cs` implement `IHttpStreamSession` / `IHttpStreamConnection` and use `HttpTransferException`, `HttpTransferMessages`, `HttpFrameLog`, `Http3StreamTrace`, `HttpStreamOpenedLines`, `HttpConnectionInfoLines`, `HttpRequestHeadFormatter`, `Http2RequestHeaders` and `Http2ResponseHead`, all curl-facing types in `Curl.Protocol.Http.UnitLibrary`. Decided: split the work. BL-4201 builds the protocol core (`Http3ClientSession`, `Http3ClientRequestStream`) in `Curl.Http3.UnitLibrary`, reporting failures as `Http3Exception` and exposing hooks for frames, trace and GOAWAY; this task then rebuilds `Http3Session` and `Http3StreamConnection` as thin adapters over it (same names, so `HttpProtocolHandler.cs` needs no change), moves the protocol-level tests to `Curl.Http3.UnitTests` and keeps the adapter tests (curl messages, exit codes, logs) where they are. Requeued with `depends-on: [BL-4201]` before any code edit.

- 2026-10-29 (lane 5): `Http3Session` is now curl's adapter over `Http3ClientSession` (its `Core`): it passes curl's SETTINGS and a control-frame callback that keeps the SETTINGS and GOAWAY log lines, maps the core's `ConnectionError` (`Http3Exception` or `QpackException`) to exit 56 with nghttp3's name, and a refused bidirectional open to exit 55. `Http3StreamConnection` reads and writes through `Http3ClientRequestStream` (`SendHeadersAsync`, `SendDataAsync`, `EndRequestAsync`, `ReadFrameOrDataAsync`, `DecodeFieldSection`). No `HttpProtocolHandler` change. Decided (sensible default, no ADR: it carries out BL-4201's recorded split, no behaviour changes): the protocol-level tests already live in `Curl.Http3.UnitTests` as `Http3ClientSessionTests` and `Http3ClientRequestStreamTests` (BL-4201, which cover any pseudo-header list and `IsExtendedConnectEnabled`), so `Http3SessionTests` and `Http3StreamConnectionTests` stay as the adapter's tests of curl's messages, logs and exit codes rather than being duplicated; no `Http3ClientStreamConnection` file was needed. Added `Curl.Http3.UnitLibrary/CLAUDE.md` to touches to say the session is no longer the handler's alone; no task in Doing on origin/work/dark-factory names it.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` found only `Http3Session.Encoder` and `Decoder` uncovered, now removed as unused. Curl.Protocol.Http.UnitTests 2623 passed; `dotnet build` 0 warnings; every fast test assembly passed except Curl.Quic.UnitTests (6 loopback `QuicConnectionTests` timed out at ~1 m 39 s, 4 alone), which references neither changed library: filed as BL-4272. Tests are in-memory only, so platform-neutral (CI criterion met by that).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: d757f172 d98609d6 478f867b 3d836d5e.
- 2026-10-29: Doing -> Backlog. Waits on BL-4201: the HTTP/3 protocol core must exist in Curl.Http3.UnitLibrary before Http3Session can be rebuilt on it
- 2026-10-29: Backlog -> Doing. Criteria at claim: d757f172 d98609d6 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. Http3Session and Http3StreamConnection are curl's adapters over Curl.Http3's public Http3ClientSession and Http3ClientRequestStream, which any caller can use to open request streams with its own pseudo-headers
