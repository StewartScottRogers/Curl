# ADR-0281 — An unreadable SSH key names the OpenSSL build's reason off Windows

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-990.

## Context

ADR-0262 gave every `publickey` attempt whose public key could not be read the reason
`Reason unknown (-1)`, as the Windows reference build (curl 8.21.0, libssh2 1.11.1 on
WinCNG) writes it. With no `--pubkey`, libssh2 derives the public key from the private key
file through its cryptography backend, and the backends fail differently.

curl 8.21.0's `lib/vssh/libssh2.c` (at `curl-8_21_0`) writes
`SSH: publickey authentication denied: %s` with `Reason unknown (-1)` only when libssh2
returned `-1`, and otherwise with `libssh2_session_last_error`'s message. WinCNG's backend
returns `-1`. libssh2 1.11.1's `src/openssl.c`, `_libssh2_pub_priv_keyfile`, returns
`LIBSSH2_ERROR_FILE` with:

- `Unable to extract public key from private key file: Unable to open private key file`
  when `BIO_new_file` cannot open the file (the empty path curl hands libssh2 when no key
  is found included);
- `Unable to extract public key from private key file: Wrong passphrase or
  invalid/unrecognized private key file format` when neither `PEM_read_bio_PrivateKey`
  nor the `openssh-key-v1` reader decodes it.

BL-681 measured the second on Ubuntu's curl 8.18.0 (libssh2 1.11.1, OpenSSL 3.5.5) against
OpenSSH 10.2 for an encrypted Ed25519 key with no `--pass`, a wrong `--pass`, and a
`chacha20-poly1305@openssh.com` key, each exit 67 `Authentication failure`. No curl 8.21.0
OpenSSL build was at hand, so its wording is taken from the source above.

## Decision

- With no `--pubkey` and a public key that cannot be derived, `SshUserAuthentication`
  gives the reason by the preset's `CryptographyBackend`: for `OpenSSL`, the unopened-file
  text when the private key file does not open and the unrecognized-format text when it
  opens; for `WinCNG`, or a preset that names no backend, `Reason unknown (-1)` as before.
- An unreadable `--pubkey` and an RSA key with no signature algorithm the server accepts
  give libssh2's own text on either backend, not `Reason unknown (-1)` (amended by
  BL-1043): `Unable to open public key file` for a `--pubkey` that does not open,
  `file_read_publickey`'s text for one that does not parse, and `No signing signature
  matched`; ADR-0262 point 5 lists them. The backend decides the reason only when, with
  no `--pubkey`, the public key cannot be derived from the private key.
- libssh2's `Unsupported private key file format` (a PEM key of a type it cannot derive)
  is not distinguished: this library reads no such key.

## Consequences

The handler's `-v` lines on Linux and macOS now say why the key was refused as the
platform's curl does; the Windows lines are unchanged.
