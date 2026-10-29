# ADR-1045 — The CoAP extension follows RFC 7252 and maps 4.xx and 5.xx codes as HTTP error statuses are mapped

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4246.

## Context

Upstream curl has no CoAP support, so the extension (ADR-0999) is tested against its specification. BL-4245
built the message codec (`Curl.Extension.Coap.UnitLibrary/Wire`); BL-4246 makes requests over UDP with it.
RFC 7252 leaves to the client how a response is shown and when it gives up, and curl has no CoAP answer to
match, so these choices follow the nearest curl behaviour, HTTP's.

## Decision

- **Scheme and version.** The extension serves `coap` alone, UDP port 5683 by default, and follows RFC 7252
  (CoAP version 1). `coaps` (DTLS 1.2, RFC 7252 section 9) comes with BL-4252, block-wise transfer of large
  bodies (RFC 7959) with BL-4247, and observing a resource (RFC 7641) with BL-4248.
- **Request.** One confirmable request: GET, or POST when `-d` (or an empty `-d ''`) is given, or the method
  `-X` names: `GET`, `POST`, `PUT` or `DELETE`, exactly as written. Any other `-X` fails with exit 2
  (`CURLE_FAILED_INIT`) before any datagram is sent: RFC 7252 defines no other method. The `-d` data is the
  payload of whatever method is sent. The Message ID and a 4-byte token come from the system random number
  generator.
- **URI options** (section 6.4). Uri-Host carries the host in lowercase unless it is an IP literal; Uri-Port is
  never sent, because the datagram goes to the URL's port; each path segment is one Uri-Path and each
  `&`-separated query argument one Uri-Query, percent-decoded to bytes.
- **Retransmission** (sections 4.2 and 4.8). The first timeout is ACK_TIMEOUT (2 s) times a random factor from 1
  to ACK_RANDOM_FACTOR (1.5), doubled on each of MAX_RETRANSMIT (4) retransmissions, all on the transfer's
  `TimeProvider`. With no answer after the last one the transfer fails with exit 28
  (`CURLE_OPERATION_TIMEDOUT`), `CoAP server did not answer after 4 retransmissions`.
- **Responses** (sections 5.2 and 5.3). A piggybacked response is the ACK with the request's Message ID and
  token. An empty ACK stops retransmission; the separate response is then the CON or NON message with the
  request's token, and a CON one is acknowledged with an empty ACK. With none within EXCHANGE_LIFETIME
  (247 s) the transfer fails with exit 28. A Reset with the request's Message ID fails with exit 56
  (`CURLE_RECV_ERROR`), `CoAP server reset the request`. Any other confirmable message is answered with a
  Reset, and anything else, including a datagram that does not decode, is ignored (section 4.2).
- **Response code mapping.** A 4.xx or 5.xx response is treated as curl treats an HTTP 4xx or 5xx status: its
  payload is written and the transfer ends with exit 0; under `-f` nothing is written and it ends with exit 22
  (`CURLE_HTTP_RETURNED_ERROR`), `The requested URL returned error: 4.04`; under `--fail-with-body` the payload
  is written and it ends with exit 22.
- **Showing the code.** `-i` (and `-D`) writes the line `CoAP/1.0 <c.dd>` and a blank line, CRLF-terminated,
  before the payload, as HTTP's status line comes before its body. `-v` reports `CoAP response <c.dd>` as an
  informational line.

## Consequences

`curl coap://host/path` works as a plain GET client and lists in `curl --extensions` and
`curl --ai-help extensions`. Options and payloads larger than one datagram need BL-4247; `coaps://` needs
BL-4252; `Observe` needs BL-4248. With `CURL_EXTENSIONS=0` the scheme is unknown and fails as curl fails one
(exit 1, `Protocol "coap" not supported`).
