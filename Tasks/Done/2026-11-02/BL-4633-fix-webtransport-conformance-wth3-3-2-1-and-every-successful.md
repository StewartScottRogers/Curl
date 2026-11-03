---
id: BL-4633
title: Fix WebTransport conformance: WTH3-3.2-1 and every successful session crash closing a disposed QUIC connection
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary, Curl.Extended.Protocol.WebTransport.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4633 — Fix WebTransport conformance: WTH3-3.2-1 and every successful session crash closing a disposed QUIC connection

## Goal

A `webtransport://` transfer whose session succeeds exits 0 with the server's answer on stdout, instead of throwing `ObjectDisposedException` from `QuicConnection.CloseAsync`.

## Context

Found by BL-4608's conformance cases (`Curl.Conformance.Extended.UnitTests/WebTransport/Cases/`). `WebTransportSession.RunAsync` ends with `await using Http3ClientSession http3`, which disposes the QUIC connection; `WebTransportProtocolHandler.TransferAsync` then calls `connection.CloseAsync((long)Http3ErrorCode.NoError, ...)` on it (`WebTransportProtocolHandler.cs` line 63) and `QuicConnection.CloseAsync` throws `ObjectDisposedException` (`Curl.Quic.UnitLibrary/QuicConnection.cs` line 123), which escapes the handler. Every case whose session succeeds fails on it, so their other assertions (SETTINGS, CONNECT fields, stream and datagram bytes, the client's WT_CLOSE_SESSION) were not yet reached: fix any further client defect they show in this task too. Listed IDs in `KnownFailures.txt`: WebTransport:WTH3-3.1-2, WTH3-3.1-3, WTH3-3.2-1, RFC9220-3-2, WTH3-4.2-1, WTH3-4.3-1, WTH3-4.6-1, RFC9297-2.1-1, WTH3-6-1, WTH3-6-3, RFC9297-3.2-1.

## Acceptance criteria

- [x] Every case in `Curl.Conformance.Extended.UnitTests/WebTransport/Cases/` carrying the IDs above passes.
- [x] A focused unit test in `Curl.Extended.Protocol.WebTransport.UnitTests` pins the fix (ADR-0482).
- [x] The IDs' `KnownFailures.txt` lines naming BL-4623 are removed; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and `Curl.Extended.Protocol.WebTransport.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Cause: `Http3ClientSession.DisposeAsync` (the `await using` at the end of `WebTransportSession.RunAsync`) already closes the connection with H3_NO_ERROR and disposes it, so the handler's own `CloseAsync` afterwards hit `QuicConnection`'s `ObjectDisposedException`. Fix: the handler no longer closes the connection itself; its `await using` dispose is a no-op on a closed `QuicConnection`. No further client defect showed once the crash was gone: all 33 WebTransport conformance cases pass, none skipped.
- Unit test: `ScriptedHttp3Connection.CloseAsync` now throws `ObjectDisposedException` when already closed or disposed, as `QuicConnection` does; `WebTransportProtocolHandlerTests.ExecuteAsync_SessionHasClosedTheConnection_DoesNotCloseItAgain` pins the fix.
- Measured: full solution build with -warnaserror clean, fast tests green, Measure-CodeQuality for the WebTransport library at 100% line, 100% branch, 0 failing members, worst CRAP 10. The change is platform-neutral (no paths, OS errors or certificates involved).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 30fb7192 bd7a9132 1b3db0b2 e6dffd78.
- 2026-11-02: Doing -> Done. A successful webtransport:// session exits 0 with the server's answer; all 11 BL-4623 WebTransport conformance cases pass
