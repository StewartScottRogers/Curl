# ADR-0726 — `CapsuleDatagramChannel` decodes received capsules by curl 8.22.0's rules

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2894
- Decided by Claude under Stewart's delegation.

## Context

Porting upstream unit program 3400 (`tests/unit/unit3400.c`) showed that Curl's
CONNECT-UDP tunnel read capsules differently from curl 8.22.0's
`Curl_capsule_process_udp_raw` (`lib/vquic/capsule.c`). Curl skipped a capsule of another
type, a context ID other than 0 and a capsule of length 0, and cut a datagram longer than the
receive buffer to it. curl fails the receive with `CURLE_RECV_ERROR` for each of these: it skips
the type byte, the header up to the context ID, or the whole oversized capsule, and returns the
error. curl reads the context ID as one byte, not as a variable-length integer.

unit3400's checks sit under `USE_PROXY_HTTP3` (and `USE_NGTCP2` for the decoder). Curl carries
HTTP/3 through a proxy on every platform (ADR-0250), so ADR-0723's platform split does not apply.

## Decision

- `CapsuleDatagramChannel.DecodeDatagram` is curl's decoder over a span of received bytes:
  `Decoded`, `NeedMoreBytes` (`CURLE_AGAIN`), `EmptyDestination` (`CURLE_BAD_FUNCTION_ARGUMENT`)
  or `Malformed` (`CURLE_RECV_ERROR`), with the bytes consumed exactly as curl skips them. A
  capsule length past `int.MaxValue` stands in for curl's `SIZE_MAX` check.
- `BuildCapsuleHeader` is curl's `capsule_encap_udp_hdr`; `BuildCapsule` is header and payload.
- `ReceiveAsync` buffers what it reads and decodes with `DecodeDatagram`; `Malformed` throws a
  `SocketException` for `SocketError.ConnectionAborted`, an empty buffer `ArgumentException`.
- The port `Unit3400Capsule` runs every check on every platform, and case 3400 is on the
  ratchet.

## Consequences

- A proxy that sends a capsule curl rejects ends Curl's QUIC receive as it ends curl's,
  rather than being ignored as RFC 9297 suggests for unknown capsule types: drop-in behaviour
  wins over the RFC's leniency, as everywhere else.
- QUIC's receive buffers are larger than any datagram a proxy sends, so the oversized-datagram
  error is not reached in normal transfers.
