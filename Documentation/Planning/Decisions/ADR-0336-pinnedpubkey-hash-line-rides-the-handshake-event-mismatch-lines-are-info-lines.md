# ADR-0336 — `--pinnedpubkey`'s `-v` hash line rides the handshake event; its mismatch lines are info lines

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-877.
This completes ADR-0193, which made `--pinnedpubkey` fail with exit 90 but printed no `-v` line.

## Context

curl's `Curl_pin_peer_pubkey` prints `infof(" public key hash: sha256//<base64>")` for a
`sha256//` pin, matching or not, and nothing for a key-file pin. Measured on 2026-10-01 with
`Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile` and `-v -k --pinnedpubkey ...` (curl 8.21.0,
Schannel):

- right hash: `* ALPN: curl offers http/1.1`, `*  public key hash: sha256//<b64>`,
  `* ALPN: server did not agree on a protocol. Uses default.`
- wrong hash: `* ALPN: curl offers http/1.1`, `*  public key hash: sha256//<server key's b64>`,
  `* SSL: public key does not match pinned public key` twice, `* closing connection #0`, and
  `curl: (90) ...`; no ALPN result line.
- the key as a PEM file: no hash line.

curl 8.18.0's OpenSSL build (BL-608) prints the hash line after
`*  SSL certificate verification failed, continuing anyway!`, after the certificate details, and
the mismatch line once.

## Decision

1. **The hash travels on `TlsHandshakeEvent.PinnedPublicKeyHash`** for a handshake that
   completes, because the Schannel build prints it between the two ALPN lines, which only the
   event's renderer (`TransferEventInfoText`) can do. The OpenSSL renderer appends it after the
   verify result. `ServerCertificateVerification.Judge` records it on `PeerVerification` once the
   certificate is accepted, so a certificate refused with exit 60 prints none, as in curl.
2. **A refused pin is reported as info lines** by `PeerVerification.ReportPinnedPublicKeyRefusal`,
   from both providers' failure paths: the hash line, then
   `SSL: public key does not match pinned public key` twice in the Schannel build (its own line
   and its error echoed) and once in the OpenSSL build (the error echoed). A failed handshake
   reports no `TlsHandshakeEvent`, and the pin is judged inside the handshake (ADR-0193), so
   there is no event to carry them.
3. **QUIC is left alone**: `QuicDialer` sets no hash and reports no mismatch lines yet.

## Consequences

- The Schannel success case and the hash and mismatch lines of a refusal now match curl byte for
  byte. Still missing on a refusal, because no failed handshake reports them: the Schannel build's
  `ALPN: curl offers` line before the hash, and the OpenSSL build's certificate details. Both are
  a gap in every failed handshake, not in the pin, and go to a follow-up task.
- The wording of the hash line lives in two places, `TransferEventInfoText` (Output) and
  `PeerVerification` (Networking), because Networking does not reference Output.

## Alternatives considered

- **Check the pin after the handshake, as curl does**, so a refusal could report a full
  `TlsHandshakeEvent`: rejected for now, because ADR-0193 put the check in `Judge` to keep both
  providers' methods under the complexity limit, and `SslStream` aborts the handshake from its
  callback anyway.
- **Report the hash as an info line on success too**: rejected, because the Schannel build prints
  it before the ALPN result line, which the event prints.
