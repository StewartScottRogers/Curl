# ADR-1216: WebTransport conformance pins SETTINGS_WT_ENABLED and exit 56 on a server's WT_CLOSE_SESSION

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4608
- Decided by Claude under Stewart's delegation.

## Context

BL-4608 measures Curl's `webtransport://` client (ADR-1089) against draft-ietf-webtrans-http3-16 with
`WebTransportReferenceServer` (BL-4607). Two points needed a decision:

1. The reference server advertised `SETTINGS_WT_MAX_SESSIONS` (`0x14e9cd29`), which earlier drafts used. Draft 16
   offers WebTransport with `SETTINGS_WT_ENABLED` (`0x2c7cf000`) = 1, which is what the client looks for.
2. The draft says what an endpoint does on a peer's `WT_CLOSE_SESSION` (end the session's streams, close the
   CONNECT stream), but not what a command-line client reports when the server ends a session before answering.

## Decision

1. The reference server's script sends `SETTINGS_WT_ENABLED` (`WebTransportServerScript.WebTransportEnabled`,
   default 1, null leaves it out) in place of `SETTINGS_WT_MAX_SESSIONS`. A fault in the reference server is fixed
   in the conformance task.
2. A server's `WT_CLOSE_SESSION` ends the transfer with exit 56 (`CURLE_RECV_ERROR`, as for any session the peer
   ends early) and `curl: (56) The server closed the WebTransport session: code <code>, <message>`; the client
   closes its side of the CONNECT stream with a FIN and sends no capsule of its own. The case
   `WebTransportCloseTests.ServerClose_WithCodeAndMessage_EndsTheTransferWithExit56AndTheClientSendsOnlyFin` pins
   it; BL-4634 implements it.

## Consequences

The client's present failures are listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`: every
successful session ends in an `ObjectDisposedException` (BL-4633) and a server close is not read (BL-4634).
