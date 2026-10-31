# ADR-1061 — DTLS flights are resent twelve times before the handshake gives up

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4264.

## Context

The CoAP extension's DTLS 1.2 handshake (`coaps://`, BL-4251) resends a flight when the peer's next flight does not
arrive (RFC 6347 section 4.2.4). Section 4.2.4.1 recommends a 1 second initial timer, doubled on each expiry up to
60 seconds, but sets no limit on the number of resends. Upstream curl has no CoAP, so no curl behaviour decides it.

## Decision

- `DtlsFlightRetransmitter` (`Curl.Extended.Protocol.Coap.Dtls.Retransmission`) starts the timer at 1 second, doubles it
  after each resend and caps it at 60 seconds, on the transfer's `TimeProvider`.
- It resends a flight at most **12** times, OpenSSL's `DTLS1_TMO_ALERT_COUNT`, the library behind curl's
  Linux and macOS builds. With the timer it waits 1+2+4+8+16+32 seconds then 60 seconds seven times, about
  8 minutes, before it gives up. A limit under 7 would give up before the 60-second cap is ever reached.
- Giving up throws `TimeoutException`; the handshake (BL-4251) turns it into exit 35 (`CURLE_SSL_CONNECT_ERROR`),
  as curl does for a TLS handshake that fails. `--connect-timeout` and `--max-time` still end the transfer sooner.
- The start of the peer's next flight stops the timer: the caller passes a task that completes when it arrives.

## Consequences

A silent DTLS server holds a `coaps://` transfer for about 8 minutes unless a timeout option is given, as OpenSSL's
own DTLS client would. The limit is one constant, `DtlsFlightRetransmitter.MaximumRetransmissions`.
