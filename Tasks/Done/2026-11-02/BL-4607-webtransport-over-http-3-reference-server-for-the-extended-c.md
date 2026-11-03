---
id: BL-4607
title: WebTransport over HTTP/3 reference server for the extended conformance harness
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4606]
touches: [Curl.Conformance.Extended.UnitLibrary/WebTransport/Server, Curl.Conformance.Extended.UnitTests/WebTransport/Server, Curl.Conformance.Extended.UnitLibrary/Curl.Conformance.Extended.UnitLibrary.csproj, Curl.Conformance.Extended.UnitTests/Curl.Conformance.Extended.UnitTests.csproj, Curl.Conformance.Extended.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4607 — WebTransport over HTTP/3 reference server for the extended conformance harness

## Goal

`Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/` holds a reference server for WebTransport over HTTP/3 (draft-ietf-webtrans-http3-16). It runs on `Http3ServerConnection` behind the harness's `IDatagramReferenceServer` UDP route and can be scripted for every case BL-4608 needs.

## Context

Split from BL-4494. Build it from the specification, never from `Curl.Extended.Protocol.WebTransport.UnitLibrary` (no reference to any `Curl.Extended.Protocol.*` project). Follow `Documentation/Conformance/Extended/README.md` and `Curl.Conformance.Extended.UnitLibrary/CLAUDE.md`, and add nothing to `Harness/`. Both extended csproj files gain a `ProjectReference` to `Curl.Conformance.Http3Server.UnitLibrary`, which brings Curl.Quic, Curl.Http3 and Curl.Tls with it. If a type the server needs is internal there, add an `InternalsVisibleTo` to that library's csproj, add the csproj to touches and say so under Notes. `Http3ServerConnection.Receive(datagram)` maps one-to-one onto `IDatagramReferenceServer.Answer`.

Each case scripts the server's behaviour:
- the SETTINGS it sends: SETTINGS_ENABLE_CONNECT_PROTOCOL, SETTINGS_H3_DATAGRAM and SETTINGS_WT_MAX_SESSIONS, each of which can be left out;
- the status of its CONNECT response;
- an echo on a bidirectional stream (the 0x41 signal and the session id);
- an echo on a unidirectional stream (type 0x54 and the session id);
- an echo of a datagram (with the quarter stream id prefix);
- `WT_CLOSE_SESSION` with a code and a message.

For the cases to assert, the server records what the client sent: its SETTINGS, the CONNECT field section (decoded with QPACK), stream and datagram bytes, and any capsule or stream FIN.

## Acceptance criteria

- [x] The server lives in `Curl.Conformance.Extended.UnitLibrary/WebTransport/Server/` with one type per file, implements `IDatagramReferenceServer` and references no `Curl.Extended.Protocol.*` project.
- [x] Each case can choose each scripted behaviour listed in Context, and the server records every client item listed there.
- [x] Unit tests in `Curl.Conformance.Extended.UnitTests/WebTransport/Server/` hold the server at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no package added; every test is platform-neutral.

## Notes

- Plan: `WebTransportReferenceServer` (an `IDatagramReferenceServer` and `IDisposable`) wraps one `Http3ServerConnection`; `Answer` feeds the datagram to `Receive`, and once the handshake is done opens the control stream (SETTINGS per `WebTransportServerScript`), classifies each client stream by its first varint (bidi 0x41 = WebTransport stream, any other bidi = the extended CONNECT; uni 0x00 = control, 0x54 = WebTransport, anything else ignored), echoes when scripted and records SETTINGS, CONNECT fields (QPACK, no dynamic table), stream bytes and FINs, capsules (DATA payloads on the CONNECT stream) and datagrams (quarter stream id + payload). No type was internal, so no `InternalsVisibleTo` was needed.
- Defaults chosen: SETTINGS_WT_MAX_SESSIONS is 0x14e9cd29 (the draft codepoint), WT_CLOSE_SESSION 0x2843, all three settings sent by default with WT_MAX_SESSIONS = 1, CONNECT status 200, echoes off by default. A status outside 2xx ends the CONNECT stream with its HEADERS and never sends WT_CLOSE_SESSION. `CloseSessionAfterEchoes` (default 0) says how many echoes (finished streams and datagrams) come before the scripted WT_CLOSE_SESSION, so a case can echo first and close after; the capsule goes in one DATA frame followed by the FIN.
- Added `Curl.Conformance.Extended.UnitLibrary/CLAUDE.md` to touches: its reference list had to name the new `Curl.Conformance.Http3Server.UnitLibrary` reference to stay true; no task in Doing names it.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary` reports 0 failing members (after splitting `ReadConnectFrame` out of `ReadConnectStream` and covering whole SETTINGS pairs). `dotnet build Curl.slnx -warnaserror` clean, fast tests green (115 test assemblies passed). Tests use a self-signed certificate made in memory and no paths, so they are platform-neutral; CI on Linux and macOS is covered by that and the green fast run here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: e94cd203 608d7561 fcf7a6c0 8466658a.
- 2026-11-02: Doing -> Done. WebTransportReferenceServer serves scripted WebTransport over HTTP/3 on Http3ServerConnection and records what the client sent
