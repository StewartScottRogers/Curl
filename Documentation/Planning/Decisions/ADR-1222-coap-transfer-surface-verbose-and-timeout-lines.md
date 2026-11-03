# ADR-1222: The CoAP transfer surface pins the DTLS verbose line and a connect-phase timeout for coaps

- Status: Accepted
- Date: 2026-11-03
- Task: BL-4627

Decided by Claude under Stewart's delegation.

## Context

BL-4627 checks curl's generic transfer options for `coap` and `coaps`
(`Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md`). curl 8.22.0 has no CoAP, so there is
no real curl to measure for two answers: which TLS line `-v` writes for a DTLS session, and what `-m` says when the
DTLS handshake never finishes. The other TLS schemes' cases match the platform curl's TLS line (`* schannel: ` on
Windows, `* SSL connection using TLSv1.` on the OpenSSL build). Curl's DTLS 1.2 is built by hand
(`Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/`) on every platform; Schannel's DTLS is not what it runs.

## Decision

- `-v` on a `coaps` transfer writes, after the connection line every transfer writes, a line starting
  `* SSL connection using DTLSv1.2`, on every platform: the line curl's OpenSSL build writes for a DTLS session
  (`SSL_get_version` gives `DTLSv1.2`), since the DTLS is the same hand-built code everywhere.
- `-m` on a `coaps` transfer whose server never answers the handshake ends with
  `curl: (28) Connection timed out after N milliseconds`, curl's message for a timeout in the connect phase, which
  takes in the TLS handshake (as a stalled TLS handshake on TCP does); a `coap` transfer, which has no handshake,
  keeps `Operation timed out after N milliseconds with 0 bytes received`. Both are what Curl writes today.
- `--fail` and `--fail-with-body` on a 4.xx or 5.xx CoAP response exit 22, as an HTTP error status does: the CoAP
  library maps the response code that way (RFC 7252 section 5.9, `CoapResponseCodeTests`).

## Consequences

`CurlSurface:Verbose-Coap-1` and `CurlSurface:Verbose-Coaps-1` fail today (`-v` writes only `* CoAP response 2.05`)
and are listed in `KnownFailures.txt` with BL-4666, which adds the connection and DTLS lines.
