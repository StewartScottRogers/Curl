---
id: BL-727
title: Close QUIC connections, time out idle ones and map QUIC errors to curl's exit codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-725, BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-727 — Close QUIC connections, time out idle ones and map QUIC errors to curl's exit codes

## Goal

The QUIC connection closes as curl's build closes it (CONNECTION_CLOSE with the application or transport code, closing and draining periods), times out after the negotiated idle timeout, recognises a stateless reset, and turns every transport error, TLS alert and timeout into the typed failure BL-718's ADR maps to curl's exit codes (96 `CURLE_QUIC_CONNECT_ERROR`, 95 `CURLE_HTTP3`, 28 timeout).

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-725 and BL-726. References: RFC 9000 section 10 (idle timeout, immediate close, stateless reset), 20 (error codes); RFC 9001 section 4.8 (TLS alerts as `CRYPTO_ERROR`).
- The failure messages `-v` and the error line print are measured in BL-718 (record them there if missing).

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` pin the CONNECTION_CLOSE sent at the end of a normal connection, the idle timeout on a fake `TimeProvider`, detection of a stateless reset token, and the typed failure and mapped exit for a peer CONNECTION_CLOSE with a transport error, a TLS alert, and a handshake that never completes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Decisions in ADR-0175, from `lib/vquic/curl_ngtcp2.c` at `curl-8_18_0` (read for this
  task): after the handshake the server's CONNECTION_CLOSE (TLS alert included), a
  transport error the client detects and a stateless reset are exit 56 `Failure when
  receiving data from the peer` (`ngtcp2_conn_read_pkt` fails, curl returns
  `CURLE_RECV_ERROR` with no message); the idle timeout is exit 55 `ngtcp2_conn_handle_expiry
  returned error: ERR_IDLE_CLOSE`; a keep-alive PING at half the idle timeout; CONNECTION_CLOSE
  sent once with no closing period; drain silently after the server's close or a reset.
  During the handshake ADR-0165's mapping (7, 8, 35, 60, 55, 28) stands.
- The goal names exits 96 and 95: in curl's source those come only from the HTTP/3 layer
  (fewer than 3 uni streams, a reset or truncated request stream), so they belong to
  `Curl.Http3.UnitLibrary`, not this library; no QUIC transport failure maps to them.
- A flow control or stream violation after the handshake was exit 7 (BL-726); it is now 56,
  as in curl's build. `QuicConnectionTests` and `QuicClientStreamsTests` updated.
- Tests: `QuicConnectionCloseTests` (normal close with `H3_NO_ERROR`, server close with a
  transport error and a TLS alert after the handshake, stateless reset and non-resets,
  keep-alive and idle timeout on `ManualTimerTimeProvider`, the 3-PTO floor, both sides'
  timeouts), `QuicHandshakeFailuresTests`, and two `QuicConnectionTests` through the loop.
  The handshake that never completes is pinned by `QuicClientConnectorTests`
  (`..._ClosesWithInternalErrorAndExit55After10Seconds`, `..._ClosesWithNoErrorAndExit28`)
  and a TLS alert or transport error closing the handshake by
  `QuicClientHandshakeTests.Receive_ServerCloseDuringTheHandshake_FailsWithTheMappedExit`.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0175; no task in Doing
  names it.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. QUIC connections close, drain, keep alive and idle out as curl's ngtcp2 build does; stateless resets are recognised; post-handshake failures map to exit 56 and 55 (ADR-0175)
