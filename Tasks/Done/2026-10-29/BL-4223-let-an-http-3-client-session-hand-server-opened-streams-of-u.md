---
id: BL-4223
title: Let an HTTP/3 client session hand server-opened streams of unknown types to its caller
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4222]
touches: [Curl.Quic.UnitLibrary/QuicConnection.cs, Curl.Quic.UnitLibrary/QuicStreamSet.cs, Curl.Quic.UnitTests, Curl.Protocol.Abstractions.UnitLibrary/IPeerBidirectionalStreamAccepting.cs, Curl.Http3.UnitLibrary/Http3ClientSession.cs, Curl.Http3.UnitLibrary/Http3PeerUnidirectionalStreams.cs, Curl.Http3.UnitLibrary/Http3UnknownPeerStream.cs, Curl.Http3.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4223 — Let an HTTP/3 client session hand server-opened streams of unknown types to its caller

## Goal

A caller of `Http3ClientSession` receives the server-opened streams HTTP/3 itself does not use - unidirectional streams of an unknown type (WebTransport's 0x54) and bidirectional streams (WebTransport's 0x41 signal) - with their type or first varint already read, instead of having them discarded or refused.

## Context

Found by BL-4113 (WebTransport extension, draft-ietf-webtrans-http3 sections 4.2 and 4.3). Today `Http3PeerUnidirectionalStreams` discards unknown stream types, and `IMultiplexedConnection` has no way to accept a peer-initiated bidirectional stream. Add accepting peer bidirectional streams as a new optional interface in `Curl.Protocol.Abstractions.UnitLibrary` (new file) that `QuicConnection` implements, so other implementations do not change, and have `QuicConnection` grant the peer bidirectional stream credit only when a caller opts in. Add to `Http3ClientSession` an optional callback (constructor parameter) that is handed such streams; without the callback behaviour is unchanged. Depends on BL-4222 because both edit `QuicConnection.cs`.

## Acceptance criteria

- [x] With the callback given, a peer unidirectional stream of an unknown type reaches it with its type; without it, the stream is discarded as RFC 9114 section 6.2 requires, as today (unit tests).
- [x] With the callback given, a peer-initiated bidirectional stream reaches it; without it, the client grants no peer bidirectional streams, as today (unit tests).
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Http3.UnitLibrary/Http3UnknownPeerStream.cs` to `touches`: the record handed to the callback needs a file of its own (one public type per file). No task in Doing on `origin/work/dark-factory` names `Curl.Http3.UnitLibrary`.
- Decision (ADR-1063): the callback is `Func<Http3UnknownPeerStream, bool>`; it returns whether it took the stream, and a declined stream is stopped with `H3_STREAM_CREATION_ERROR`, so RFC 9114 section 6.2's behaviour holds for whatever the caller does not use. Grease types are offered too; the caller declines them.
- Decision (ADR-1063): the QUIC transport parameters stay curl's (`initial_max_streams_bidi` 262144) so the Initial stays byte for byte curl's; the opt-in is the session accepting peer bidirectional streams only when the callback is given and the connection implements `IPeerBidirectionalStreamAccepting`. `QuicStreamSet` needed no change (`AcceptBidirectional` already existed).
- `Http3PeerUnidirectionalStreams.ReadTypeAsync` added beside `AcceptAsync`, which keeps its contract for `Curl.Protocol.Http`'s `Http3Session`.
- Not done here: `Curl.Engine`'s `SocketCallbackMultiplexedConnection` does not forward the new interface (ADR-1063, Consequences); BL-4113 forwards it if its path needs it.
- Measure-CodeQuality.ps1 -Library Curl.Http3.UnitLibrary,Curl.Quic.UnitLibrary: Quic 0 failing; Http3 had one uncovered branch from a nullable enum cast, fixed, rerun 0 failing (100/100, worst CRAP 10). `Curl.Protocol.Abstractions.UnitLibrary` gained only an interface with no executable lines, so it was not measured (it reaches nearly every test project).
- CI on Linux and macOS: the new tests use in-memory streams and the in-memory QUIC server only, so they are platform-neutral.

## Log

- 2026-10-29: Created by BL-4113's lane 7 run as its prerequisite.
- 2026-10-29: Backlog -> Doing. Criteria at claim: cc7d0326 f50c33e2 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. Unknown peer unidirectional and peer bidirectional streams reach an opt-in Http3ClientSession callback; gates met, build clean, fast tests green
