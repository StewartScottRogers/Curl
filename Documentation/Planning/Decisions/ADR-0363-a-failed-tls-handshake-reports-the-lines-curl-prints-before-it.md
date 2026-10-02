# ADR-0363 — A failed TLS handshake reports the `-v` lines curl prints before its failure

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1149.
Follows ADR-0336 (BL-877), which left both TLS providers reporting a `TlsHandshakeEvent` only
for a completed handshake.

## Context

Measured 2026-10-02 against `Record-CurlExchange.ps1 -Tls` (BL-1149 Notes):

- curl 8.21.0, Schannel, `-v -k --pinnedpubkey sha256//<wrong>`: `* ALPN: curl offers http/1.1`
  right after `* schannel: using IP address, SNI is not supported by OS.`, then
  `*  public key hash: sha256//...`, then the mismatch line twice, exit 90. With `-v` and the
  untrusted certificate: the same ALPN line, then the `SEC_E_UNTRUSTED_ROOT` line, exit 60.
  Schannel prints its ALPN offer before it sends the ClientHello, whatever happens after.
- curl 8.18.0, OpenSSL 3.5.5, the same pin refusal: the ALPN offer, the TLS message lines,
  `SSL connection using ...`, `ALPN: server did not agree ...`, `Server certificate:` and its
  details, ` SSL certificate verification failed, continuing anyway!`, the hash line, and the
  mismatch line once. OpenSSL finishes the handshake before curl checks the pin.

Curl printed only the hash and mismatch lines.

## Decision

- `TlsHandshakeEvent.Failed` marks a handshake that failed. `TransferEventInfoText` words a
  failed one for the Schannel build as the ALPN offer and the hash line, without the server's
  ALPN answer; for the OpenSSL build as a completed one, every line.
- `SslStreamTlsProvider` keeps what was negotiated when its certificate callback ran
  (version, suite, ALPN answer) and, on a failure, reports a failed event: always in the
  Schannel build, and in the OpenSSL build when `--pinnedpubkey` refused the key.
- `HandBuiltTlsProvider` reports a failed event in the Schannel build only: a failed
  hand-built handshake keeps no version or suite, so its OpenSSL build keeps the hash line
  alone, as before.
- `PeerVerification.ReportPinnedPublicKeyRefusal` leaves the hash line out when the failed
  event carries it.

## Consequences

- The Schannel build now prints its ALPN offer before every handshake failure, exit 60 and
  exit 35 included.
- The OpenSSL build still prints nothing of the handshake before an exit 60 or exit 35, and
  the hand-built provider's OpenSSL build nothing but the hash before an exit 90; BL-1178
  covers both.

## Alternatives considered

- Reporting the ALPN offer as a plain info line from the providers: it would put curl's
  wording in `Curl.Networking`, where `TransferEventInfoText` owns it.
- Reporting the OpenSSL build's lines for every verification failure: its exit 60 lines
  (the verify result wording without `-k`, the error echo) were not measured in this task.
