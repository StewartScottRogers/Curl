# ADR-1042 — The CoAP extension frames DTLS 1.2 itself, on Curl.Tls's types

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4250.

## Context

CoAP over DTLS (`coaps://`, RFC 7252 section 9) needs the DTLS 1.2 record layer and handshake
framing of RFC 6347. No curl protocol uses DTLS, so it lives in the extension (BL-4116), which may
reference the shared layer `Curl.Tls.UnitLibrary` (ADR-0999). The RFC leaves some choices open.

## Decision

- `Curl.Extended.Protocol.Coap.UnitLibrary` references `Curl.Tls.UnitLibrary` and reuses its
  `TlsContentType` and `HandshakeType`. hello_verify_request (type 3), which TLS never sends, is
  `DtlsHelloVerifyRequest.MessageType`, a cast rather than a new member of Curl.Tls's enum, so
  Curl.Tls is unchanged.
- The wire types in `Dtls/Wire` read bytes with `System.Buffers.Binary` and throw
  `FormatException`, as the CoAP message codec beside them does, rather than through `TlsReader`'s
  alert results: the handshake driver above them maps a format error to its alert.
- The anti-replay window holds 64 sequence numbers, the minimum RFC 6347 section 4.1.2.6
  recommends, in one `ulong` bitmap. A record older than the window is a replay.
- Reassembly keeps one byte array and one arrival flag per byte of the message, so fragments may
  arrive in any order, repeated or overlapping. Overlapping bytes that differ from those already
  received are a format error rather than silently overwritten.
- A fragment split for a size limit carries at most that many body bytes; an empty message is one
  empty fragment.

## Consequences

The record cipher, the handshake flight state machine and retransmission build on these types in
later tasks.
