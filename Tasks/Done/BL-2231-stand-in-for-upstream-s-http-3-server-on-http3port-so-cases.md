---
id: BL-2231
title: Stand in for upstream's HTTP/3 server on %HTTP3PORT so cases 2500-2503 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2246, BL-2247]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.Http3Server.UnitLibrary, Curl.Conformance.Http3Server.UnitTests, Curl.Conformance.Http2Server.UnitLibrary, Curl.slnx]
model: opus
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2231 — Stand in for upstream's HTTP/3 server on %HTTP3PORT so cases 2500-2503 run

## Goal

Stand in for upstream's `http/3` server (nghttpx with QUIC in front of `sws`) on `%HTTP3PORT`, so cases 2500, 2501, 2502 and 2503 run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; running `Curl.Conformance.UnitTests` on 2026-10-14 names these four for `%HTTP3PORT`. The gap tool uses `Curl.Conformance.UnitLibrary`. Stewart's standing rule: QUIC and HTTP/3 are hand-built, nothing is left out because it is hard.

Curl's HTTP/3 client is in the QUIC/HTTP/3 libraries (find them in `Curl.slnx`; names containing Quic or Http3). The stand-in is the server side: it must answer the client's QUIC Initial (TLS 1.3 inside QUIC with `test-localhost.pem`, ALPN `h3`), run QUIC packet protection, streams and flow control, and map each HTTP/3 request stream (QPACK) to an HTTP/1.1 request relayed to an `sws` emulation connection. Reuse the QUIC crypto and framing code the client has through a new `Curl.Conformance.Http3Server.UnitLibrary` (+ `.UnitTests`) modelled on `Curl.Conformance.SshServer.UnitLibrary` (ADR-0456), since this library may not reference protocol libraries; it connects through `IDatagramConnector` (as `TftpServerConnector` does) so it needs no socket. Register the project in `Curl.slnx` (already in this task's `touches`). This is large: if one lane run cannot finish, deliver the handshake and a GET first and file the rest (POST, QPACK dynamic table, 0-RTT) as follow-up tasks; read https://github.com/curl/curl/blob/curl-8_22_0/tests/data/test2500 .. test2503 first to see which behaviour they use.

## Acceptance criteria

- [x] The new library exists, listed in `Curl.slnx`, with `.UnitTests`, 100% line and branch coverage, complexity at most 10, CRAP at most 30, AOT-compatible; `dotnet build -warnaserror` is clean.
- [x] A datagram open to the stand-in completes a QUIC handshake with Curl's client and serves a GET and a POST through to `sws`; tests pin handshake, request, response and connection close.
- [x] Moved to BL-2261 (2026-10-14 split, see Notes): `%HTTP3PORT` has a value; screening lets `http/3` cases run; cases 2500-2503 are no longer screened out, passing ones added to `PassingUpstreamCases.txt`, failing ones with follow-up tasks.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1` shows no member outside a gate in `Curl.Conformance.Http3Server.UnitLibrary`; `dotnet test --filter "TestCategory!=Integration"` is green; the library's `CLAUDE.md` is updated (the conformance library's `CLAUDE.md` changes with BL-2261, which wires it).

## Notes

- 2026-10-14 (lane 1, first pass before any code, per AF-0147): the stand-in needs two
  prerequisites no task covered, so the task is split and requeued once:
  - BL-2246: an I/O-free TLS 1.3 *server* handshake. None exists: `Curl.Tls.UnitLibrary`
    is client-only, and the conformance TLS stand-ins use `SslStream`, which cannot hand
    out per-level handshake bytes and secrets as QUIC needs. It also creates the new
    library and its `Curl.slnx` entries.
  - BL-2247: an I/O-free QUIC server connection (Initial/Handshake/1-RTT packets, CRYPTO,
    ACK, STREAM, HANDSHAKE_DONE, CONNECTION_CLOSE) on `Curl.Quic`'s codecs.
  - What is left here once both are Done: the HTTP/3 layer (control stream and SETTINGS,
    QPACK static-table HEADERS on request streams via `Curl.Http3`'s codecs), relaying each
    request stream to an `sws` emulation as HTTP/1.1 and its reply back as HEADERS + DATA,
    an `Http3ServerConnector` over `IDatagramConnector` (as `TftpServerConnector`), a
    `%HTTP3PORT` value, and lifting screening for `http/3` cases.
- Upstream cases (curl-8_22_0): 2500 is a GET with `--resolve` and needs features
  `http/3` and `nghttpx-h3`; 2503 is a GET with `-w %header{}` and needs `nghttpx-h3` and
  `headers-api`; 2501 is a POST (features `Debug`, `http`, `http/3`, server `http/3`);
  2502 is a multi-transfer verbose-log case (`<data1..3>`). Screening must report
  `nghttpx-h3` once the stand-in is wired, and 2502 may be a libtest case the harness
  screens for another reason - check that when wiring.
- 2026-10-14 (lane 1, second claim): the first pass found one more prerequisite no task
  covered: in-process Curl's `QuicDialer` opens real UDP sockets (`UdpChannelOpener`), so the
  injected `IDatagramConnector` never sees `--http3` traffic and no case could reach a stand-in.
  Filed BL-2260 for that, and BL-2261 (depends on BL-2231 and BL-2260) for the wiring
  (`%HTTP3PORT`, `UpstreamHttp3Server`, screening, `nghttpx-h3`, cases 2500-2503). This run
  built the stand-in itself: `Http3ServerSession`, `Http3ServerRequestRelay`,
  `Http3ServerChannel`, `Http3ServerConnector` and `UpstreamHttp3ServerFactory`.
- Decisions (sensible defaults): the relay's request format follows the upstream cases'
  expected `<protocol>`, not the h2c stand-in's: absolute-form request line, `Via: 3 nghttpx`,
  no `X-Forwarded-Proto` (2501 pins all three). The reply side reuses the h2c stand-in's
  HTTP/1.1 reader through `InternalsVisibleTo` rather than copying it; those csproj lines are
  why `Curl.Conformance.Http2Server.UnitLibrary` was added to `touches` (no task in Doing on
  `origin/work/dark-factory` names it). QPACK runs with a dynamic table of capacity 0 both
  ways (an empty SETTINGS frame), which is all the cases need; a request that does not decode
  closes the connection with `H3_MESSAGE_ERROR`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary`:
  100% line, 100% branch, 124 members, 0 failing, worst CRAP 10.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Backlog. Needs BL-2246 (TLS 1.3 server handshake) and BL-2247 (QUIC server connection) first; filed and added to depends-on
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. HTTP/3 stand-in (session, nghttpx-h3 relay, datagram connector, factory) built at 100% coverage; wiring split to BL-2261 after BL-2260
