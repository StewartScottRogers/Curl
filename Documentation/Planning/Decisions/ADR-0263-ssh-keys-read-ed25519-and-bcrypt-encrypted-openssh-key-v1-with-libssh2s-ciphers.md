# ADR-0263 — SSH keys read Ed25519 and bcrypt-encrypted `openssh-key-v1` with libssh2's ciphers

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-681.

## Context

ADR-0122 and ADR-0230 decided that `--key` reads every format either SSH backend reads, on
every platform, and left Ed25519 keys and encrypted `openssh-key-v1` keys (OpenSSH's
`PROTOCOL.key`, KDF `bcrypt`) to BL-681. The primitives are hand-built in
`Curl.Cryptography.UnitLibrary`: `Ed25519` (BL-672), `BcryptPbkdf` (BL-674) and `AesCtr`
(BL-737). The rest are the BCL's.

Measured 2026-09-29, `sftp://127.0.0.1:2281/…` with `-u <user>:wrong` against OpenSSH
10.2p1 run unprivileged in WSL (`publickey,password` offered, the key in its
`AuthorizedKeysFile`), keys written by `ssh-keygen -a 16` (OpenSSH 10.3):

| Key and `--pass` | Ubuntu curl 8.18.0, libssh2 1.11.1 OpenSSL 3.5.5 | Windows curl 8.21.0, libssh2 1.11.1 WinCNG (BL-568) |
| --- | --- | --- |
| Ed25519, unencrypted | authenticated, exit 0 | not read: `Reason unknown (-1)`, exit 67 `Authentication failure` |
| Ed25519, `aes256-ctr`, right | authenticated, exit 0 | not read, exit 67 |
| Ed25519, `aes256-gcm@openssh.com`, right | authenticated, exit 0 | not measured; WinCNG reads no `openssh-key-v1` Ed25519 key |
| ECDSA P-256, `aes256-gcm@openssh.com`, right | authenticated, exit 0 | not read, exit 67 |
| Ed25519, `aes256-ctr` or GCM, wrong or no `--pass` | `Unable to extract public key from private key file: Wrong passphrase or invalid/unrecognized private key file format`, then the password, exit 67 `Authentication failure` | not read, exit 67 `Authentication failure` |
| Ed25519, `chacha20-poly1305@openssh.com`, right | as a wrong passphrase, exit 67 | not read, exit 67 |
| wrong `--pass` with a right `--pubkey` | `Callback returned error`, exit 67 | `Callback returned error`, exit 67 |

## Decision

1. **`Ed25519SshPrivateKey` signs `ssh-ed25519`** (RFC 8709) with the hand-built `Ed25519`,
   read from `openssh-key-v1` (the public key, then the seed and the public key again, which
   must agree) and from PKCS #8 (RFC 8410's `CurvePrivateKey`, OID 1.3.101.112). Its
   signature algorithm is its key type; `server-sig-algs` upgrades only RSA (ADR-0230).
2. **`OpenSshPrivateSectionDecryption` opens a `bcrypt` section** with the ciphers both
   OpenSSH writes and libssh2 1.11.1 reads: `aes128-ctr`, `aes192-ctr`, `aes256-ctr`, the same
   three in CBC, `3des-cbc`, `aes128-gcm@openssh.com` and `aes256-gcm@openssh.com` (tag after
   the section). `chacha20-poly1305@openssh.com` and any other pairing of cipher and KDF are
   not read, as libssh2 does not read them.
3. **A wrong passphrase is an unreadable key.** The check integers differ (or the GCM tag
   fails, or bcrypt-pbkdf refuses an empty passphrase), `SshPrivateKeyReader` returns no key,
   and `SshUserAuthentication` takes the path BL-568 measured for an unreadable key: no
   `publickey` request, then the password, exit 67 `Authentication failure`, the same on both
   builds.
4. **Every platform reads them**, the Windows build included, as ADR-0122 decided for the
   superset. The `-v` reason for an unreadable key stays WinCNG's `Reason unknown (-1)`; the
   OpenSSL build's reason is BL-989's.

## Alternatives considered

- **Read only what WinCNG reads on Windows.** Rejected by ADR-0122: a user's Ed25519 key
  would fail where the Linux and macOS builds accept it, and no script relies on that failure.
- **Read `chacha20-poly1305@openssh.com` keys too.** The hand-built `ChaCha20` and `Poly1305`
  would do it, but libssh2 1.11.1 refuses such a key, so reading it would authenticate where
  curl does not.

## Consequences

- `Curl.Protocol.Ssh.UnitTests` hold the keys `ssh-keygen` wrote as throwaway test data
  (`TestUserKeys`), pin the Ed25519 signature blob over a fixed session identifier, and drive
  an encrypted Ed25519 key end to end through `InMemorySshServer`.
- BL-989 makes the unreadable key's `-v` reason follow the backend.
