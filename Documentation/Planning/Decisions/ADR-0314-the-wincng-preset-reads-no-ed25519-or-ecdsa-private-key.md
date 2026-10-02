# ADR-0314 — The WinCNG preset reads no Ed25519 or ECDSA private key, as Windows curl's libssh2 does

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1098.
Narrows ADR-0122 and ADR-0230, which read every key format on every platform, for two key
types on the WinCNG preset only.

## Context

libssh2 1.11.1 built on WinCNG has no Ed25519 and no ECDSA (`LIBSSH2_ED25519` and
`LIBSSH2_ECDSA` are 0), so Windows curl cannot read or sign with such a private key in any
file format. Measured 2026-10-01 against OpenSSH 10.2p1's sshd (the Ubuntu packages
extracted into WSL, run unprivileged on a loopback port) with
`curl -sS -v -k -u user: --key K [--pubkey K.pub] sftp://127.0.0.1:2298/x.txt`:

| `--key` | Windows curl 8.21.0 (WinCNG), `--pubkey` | Windows, no `--pubkey` | Ubuntu curl 8.18.0 (OpenSSL) |
| --- | --- | --- | --- |
| Ed25519 (`openssh-key-v1`) | asks, `PK_OK`, `Callback returned error` (67) | `Reason unknown (-1)`, no request (67) | authenticates |
| ECDSA P-256 (`openssh-key-v1`, SEC 1 PEM, PKCS #8) | asks, `PK_OK`, `Callback returned error` (67) | `Reason unknown (-1)`, no request (67) | authenticates |
| RSA PKCS #1 PEM | authenticates | authenticates | authenticates |
| RSA `openssh-key-v1` or PKCS #8 | `Callback returned error` (67) | `Reason unknown (-1)` (67) | authenticates |

Each denial is followed by curl's agent attempt and then `password`.

## Decision

- On the preset whose `CryptographyBackend` is `WinCNG`, an Ed25519 or ECDSA (any curve)
  private key is not read: with `--pubkey` the question is asked and, after `PK_OK`, the
  method fails with `Callback returned error` and no signed request; without it nothing is
  sent and the method fails with `Reason unknown (-1)` (BL-990's path).
  `SshUserAuthentication.BackendReadsPrivateKey` holds the rule.
- The OpenSSL preset, the `Full` preset and every other key type keep signing.
- The key-type rule is matched because it is the backend's missing algorithm: no format
  could make Windows curl read such a key. WinCNG's narrower *format* reading (only PKCS #1
  RSA PEM, so an RSA key in `openssh-key-v1` or PKCS #8 fails too) stays unmatched, as
  ADR-0122 and ADR-0230 decided: Curl reads every format either backend reads.

## Consequences

- A Windows script that relied on curl failing with an Ed25519 or ECDSA key sees the same
  failure and the same `-v` lines from Curl.
- A user with such a key on Windows still cannot authenticate with it through Curl's
  Windows preset, exactly as with Windows curl.
