# ADR-0177 — A QUIC connection closes, times out and resets as curl's ngtcp2 build does, and fails after the handshake with exit 56 or 55

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-727.

## Context

BL-727 finishes the QUIC transport's end of life in `Curl.Quic.UnitLibrary`: the close it
sends, the server's close, the idle timeout (RFC 9000 section 10.1), and the stateless reset
(section 10.3). ADR-0144 section 7 maps the handshake's failures to exit codes, and
ADR-0165 builds them; it does not say what a failure after the handshake is. The source is
`lib/vquic/curl_ngtcp2.c` at tag `curl-8_18_0`, read for this decision:

- `cf_progress_ingress`: when `ngtcp2_conn_read_pkt` fails, the result is
  `CURLE_PEER_FAILED_VERIFICATION` for `NGTCP2_ERR_CRYPTO` and `CURLE_RECV_ERROR`
  otherwise, with no `failf`. A CONNECTION_CLOSE from the server and a stateless reset
  both make it return `NGTCP2_ERR_DRAINING`, and a transport error the client detects
  returns that error, so each is exit 56.
- `cf_ngtcp2_connect` turns that 56 into 7, or 8 for `CONNECTION_REFUSED`, only while
  connecting and in the draining period: the rows of ADR-0144 section 7 that BL-724 built.
- `check_and_set_expiry`: when `ngtcp2_conn_handle_expiry` fails it prints
  `ngtcp2_conn_handle_expiry returned error: <ngtcp2_strerror>` and returns
  `CURLE_SEND_ERROR`. The handshake timeout measured in ADR-0144 is this path with
  `ERR_HANDSHAKE_TIMEOUT`; the idle timeout is the same path with `ERR_IDLE_CLOSE`.
- `cf_ngtcp2_setup_keep_alive`: when the server declares a `max_idle_timeout`, curl sets
  ngtcp2's keep-alive to half of it, so a PING goes out while a transfer waits.
- `cf_ngtcp2_shutdown`: one `ngtcp2_conn_write_connection_close` datagram, flushed, and
  shutdown is done; curl does not wait out a closing period.

## Decision

1. **After the handshake, a lost connection is exit 56** with curl's message for it,
   `Failure when receiving data from the peer` (curl prints no message of its own on this
   path): a CONNECTION_CLOSE from the server of any kind, a TLS alert (`CRYPTO_ERROR`)
   included, a transport error the client detects in what the server sent, and a
   stateless reset. `QuicHandshakeFailure.ServerClose` carries the server's frame, so a
   caller can still tell which close it was. During the handshake the existing mapping
   stands: 7, 8 for `CONNECTION_REFUSED`, 35 or 60 for TLS.
2. **Closing and draining.** The client sends CONNECTION_CLOSE once and then nothing:
   no closing period in which it answers late packets with another CONNECTION_CLOSE, as
   curl's shutdown does not. After the server's CONNECTION_CLOSE or a stateless reset the
   client drains: it sends nothing, not even a CONNECTION_CLOSE in reply (RFC 9000
   section 10.2.2 allows but does not require one, and ngtcp2 sends none).
3. **Idle timeout.** Once the handshake is complete the timeout is the smaller
   `max_idle_timeout` of the two sides, or the one that is not 0 (curl declares 0, so in
   practice the server's), and at least three probe timeouts including `max_ack_delay`.
   It restarts when a packet from the server is opened and when the first ack-eliciting
   packet since then is sent. When it passes, the connection closes silently with
   exit 55 and `ngtcp2_conn_handle_expiry returned error: ERR_IDLE_CLOSE`. There is no
   idle timeout before the handshake completes; the handshake timeout (10 s or
   `--connect-timeout`) covers that time.
4. **Keep-alive.** Half the idle timeout after the connection, or the last keep-alive,
   was active, the client sends a PING. curl turns keep-alive off while no request stream
   is open, which the QUIC layer cannot see (HTTP/3's control streams never close); a
   `QuicConnection` exists only while a transfer uses it, so the PING runs whenever the
   connection is open.
5. **Stateless reset.** A datagram in which no packet could be opened is a stateless
   reset when it is at least 21 bytes, begins with a short header's form bit, and ends in
   the stateless reset token of the connection ID in use, compared in fixed time. Tokens
   of connection IDs the client never sent to, or has retired, are not checked
   (RFC 9000 section 10.3.1).

## Consequences

- `QuicClientHandshake` gains `TimeUntilIdleTimer` and `OnIdleTimer`, which
  `QuicConnection`'s loop runs beside the loss detection timer, and
  `QuicPeerConnectionIds.IsStatelessReset`.
- A flow control or stream violation after the handshake, which BL-726 reported as exit 7,
  is now exit 56, as curl's build reports it.
- The HTTP/3 layer (BL-730 and later) reports `MultiplexedConnectionFailedException` as
  the transfer's failure; with these messages its error line matches curl's.
