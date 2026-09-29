# ADR-0193 — `--pinnedpubkey` is checked in the shared certificate judgement, on every platform

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-608.

## Context

BL-607 parses `--pinnedpubkey` into `CommandLineOptions.PinnedPublicKey`, verbatim. curl
compares the server certificate's SubjectPublicKeyInfo with it in `Curl_pin_peer_pubkey`
(`lib/vtls/vtls.c`), which every TLS backend calls: a value starting `sha256//` is a list of
base64 SHA-256 hashes separated by `;sha256//`; anything else is a file holding the key as
DER, or as a PEM `PUBLIC KEY` block. A mismatch is exit 90, `CURLE_SSL_PINNEDPUBKEYNOTMATCH`.

Measured on 2026-09-29 with `Record-CurlExchange.ps1 -Tls -TlsPublicKeyFile` (extended in
BL-608 to write the served key and substitute `{TlsPublicKeySha256}`), with `-sS -k`:

| Case | curl 8.21.0 Schannel (Windows) | curl 8.18.0 OpenSSL 3.5.5 (Ubuntu, WSL) |
| --- | --- | --- |
| the right `sha256//` hash | exit 0 | exit 0 |
| a wrong hash | exit 90 | exit 90 |
| a wrong hash then the right one, `;`-separated | exit 0 | exit 0 |
| the key as a PEM file | exit 0 | exit 0 |
| the key as a DER file | exit 0 | exit 0 |
| another key's PEM file | exit 90 | exit 90 |
| a file holding no key | exit 90 | exit 90 |
| a missing file | exit 90 | exit 90 |
| `sha256//`, and `sha256//!!!` | exit 90 | (not run) |
| the right hash without `-k`, self-signed | exit 60 | (not run) |

Every exit 90 printed `curl: (90) SSL: public key does not match pinned public key`, in both
builds. So the Schannel build supports both pin forms, `-k` does not skip the pin, and
verification is judged before the pin.

## Decision

- `TlsClientOptions.PinnedPublicKey` carries the value; `Curl.Console` maps the target's
  `--pinnedpubkey` onto it (`--proxy-pinnedpubkey` is BL-611's).
- `PinnedPublicKey` implements `Curl_pin_peer_pubkey` and `pubkey_pem_to_der` as curl
  8.21.0 writes them: hashes compared whole and case-sensitively; a file over 1 MiB, unreadable
  or missing matches nothing; a file the key's own size is compared as DER, a larger one read
  as PEM (begin line at the start of a line, end line after a newline, CR and LF dropped, strict
  base64).
- The check runs in `ServerCertificateVerification.Judge`, after `VerifyPeer` accepts (or under
  `-k`), so a certificate that fails verification is still exit 60. `Judge` is what
  `SslStreamTlsProvider`'s validation callback and `HandBuiltCertificateVerifier` both call, so
  both TLS providers, and the hand-built QUIC handshake, honour the pin on every platform with
  one implementation. A mismatch fails the handshake from inside, where curl completes it and
  then closes; the transfer's exit code and message are the same.
- The message is `TlsFailureMessages.PinnedPublicKeyMismatch`, the same for both builds.
- The `-v` lines (`public key hash: sha256//...`, the mismatch lines, and their order, which
  differs by build) are BL-877's.

## Alternatives considered

- **Check after the handshake in each provider.** Closer to curl's order, but it adds a
  branch to two methods already at the complexity limit, duplicates the check, and leaves the
  QUIC path out. Its only visible difference is the TLS alert the server sees.
- **Skip the pin under `-k`.** Refuted by measurement: both builds check it.

## Consequences

- A pinned transfer fails with exit 90 exactly where curl does, on Windows, Linux and macOS.
- `-v` output for a pin is incomplete until BL-877.
