# ADR-0504 — The Schannel build judges `--pinnedpubkey` before a `--cacert` chain

- **Status:** Accepted
- **Date:** 2026-10-15

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2270.

## Context

ADR-0193 measured that curl judges the certificate before the pin, and
`ServerCertificateVerification.Judge` checked `--pinnedpubkey` only once `VerifyPeer` accepted.
Its table had no `--cacert` chain. With `--cacert`, curl's Schannel build turns off Schannel's own
validation and checks the chain itself after the handshake (`Curl_verify_certificate`), and per
ADR-0321 a private CA with no revocation endpoint fails that check with exit 60, "the revocation
status is unknown". Upstream cases test2035, test2038 and test2042 run
`--cacert test-ca.crt --pinnedpubkey <wrong key>` with no `--ssl-revoke-best-effort` and expect
exit 90; Curl gave 60 (gap finding GF-0079).

Measured on 2026-10-15 with `Record-CurlExchange.ps1 -Tls -TlsRootCertificateFile`, curl 8.21.0
Schannel, `-sS --cacert root.pem`:

| `--pinnedpubkey` | exit |
| --- | --- |
| a wrong `sha256//` hash | 90, `SSL: public key does not match pinned public key` |
| the server key's hash | 60, `schannel: the revocation status is unknown` |
| none | 60, the same |

So with `--cacert` the pin is judged before the manual chain check.

## Decision

- In the Schannel build with `--cacert`, `Judge` checks the pin first: a refused pin is exit 90
  whatever the chain; an accepted pin is followed by the usual verdict.
- The OpenSSL build, and the Schannel build against the system store (where Schannel validates
  inside the handshake, ADR-0193's exit 60 row), keep verification before the pin.

## Consequences

- test2035, test2038 and test2042 answer exit 90 as curl 8.22.0 does.
- The `-v` hash line is now also reported for a Schannel `--cacert` chain that is refused, since
  curl ran the pin check before it.
