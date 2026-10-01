# ADR-0313 — A key-file certificate signs with its `--key` private key under the plain method, as libssh2 1.11.1 does

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1097.
Follows ADR-0311, which chose the certificate method; this one lets a key file sign it.

## Context

`curl --key k --pubkey k-cert.pub` sends an OpenSSH certificate as the public key. libssh2
1.11.1's `sign_fromfile` reads the private key file with the host key method named by the
chosen method (`ssh-rsa-cert-v01@openssh.com` reads an RSA key, and so on) and signs with
that method's plain form. Curl compared the private key's type with the certificate's, which
never match, so every key-file certificate failed with `Callback returned error`.

Measured 2026-10-01 against OpenSSH 10.2p1's sshd (the Ubuntu `openssh-server` package run
unprivileged in WSL on a loopback port, `TrustedUserCAKeys` naming an `ssh-keygen` CA,
`PubkeyAcceptedAlgorithms +ssh-rsa-cert-v01@openssh.com` so WinCNG's method is accepted),
`curl -sS -v -k -u user: --key K --pubkey C sftp://127.0.0.1:2299/x.txt`:

| `--key` / `--pubkey` | Ubuntu curl 8.18.0 (libssh2 1.11.1, OpenSSL) | Windows curl 8.21.0 (libssh2 1.11.1, WinCNG) |
| --- | --- | --- |
| RSA / its certificate | query `rsa-sha2-512-cert-v01@openssh.com`, authenticated | query `ssh-rsa-cert-v01@openssh.com`, authenticated |
| Ed25519 / its certificate | query `ssh-ed25519-cert-v01@openssh.com`, authenticated | query asked, then `Callback returned error` |
| ECDSA P-256 / its certificate | query `ecdsa-sha2-nistp256-cert-v01@openssh.com`, authenticated | query asked, then `Callback returned error` |
| another RSA key / the RSA certificate | signed, refused: `Invalid signature for supplied public key, or bad username/public key combination` | the same |
| Ed25519 key / the RSA certificate | query asked, then `Callback returned error` | the same |

sshd accepted each signature, so each was in the certificate's plain form (`rsa-sha2-512`,
`ssh-rsa`, `ssh-ed25519`, `ecdsa-sha2-nistp256`). WinCNG's failure for Ed25519 and ECDSA is
not about certificates: a plain Ed25519 or ECDSA key fails the same way on that build
(BL-1098).

## Decision

- `SshUserAuthentication.SendSignedPublicKeyAsync` accepts a private key whose type is the
  public key's plain type (`PlainMethods`, the table ADR-0271 uses for an agent's
  signature), and signs under the chosen method's plain form.
- A certificate of another key of the same type is signed anyway and left to the server,
  as libssh2 does; a private key of another type fails the method before anything is sent.

## Consequences

- RSA, Ed25519 and ECDSA certificates authenticate from key files on every preset; WinCNG's
  inability to read Ed25519 and ECDSA private keys is BL-1098's.
- `SshUserAuthenticationTests` (`KeyFileCertificate`) pin each measured row.
