# ADR-0311 — An RSA certificate identity signs with the `rsa-sha2-*-cert-v01@openssh.com` method on OpenSSL, as libssh2 1.11.1 does

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1036.
Extends ADR-0230's and ADR-0271's signature-algorithm upgrade, which covered only `ssh-rsa`.

## Context

libssh2 1.11.1's `_libssh2_key_sign_algorithm` (`src/userauth.c`) upgrades a key's method
from the server's `server-sig-algs` with the list its crypto backend's
`_libssh2_supported_key_sign_algorithms` returns. The OpenSSL backend (`src/openssl.c`)
returns `rsa-sha2-512,rsa-sha2-256,ssh-rsa` for both `ssh-rsa` and
`ssh-rsa-cert-v01@openssh.com`; the WinCNG backend (`src/wincng.c`) only for `ssh-rsa`. A
certificate's match gets `-cert-v01@openssh.com` appended. Before matching, a server banner
whose text after `OpenSSH_` reads as a version older than 7.8 (`is_version_less_than_78`:
`strtol` up to a dot, then the minor's first digit) leaves the certificate's method alone
(`SSH_BUG_SIGTYPE`). The agent's sign flags come from the exact method, so a certificate
method asks with flag 0, and `plain_method` names the plain `rsa-sha2-*` in the signature.

Measured 2026-10-01 with a throwaway loopback server built from this library's
`InMemorySshServer` (sending `SSH_MSG_EXT_INFO` and recording each `publickey` request's
algorithm), an RSA key and an `ssh-keygen -s` certificate, `curl -sS -v -k -u tester:
--key k --pubkey k-cert.pub sftp://.../x`:

| `server-sig-algs` | Banner | Windows curl 8.21.0 (libssh2 1.11.1, WinCNG) | Ubuntu curl 8.18.0 (libssh2 1.11.1, OpenSSL) |
| --- | --- | --- | --- |
| `ssh-ed25519,rsa-sha2-512,rsa-sha2-256,ssh-rsa` | `OpenSSH_9.7` | query `ssh-rsa-cert-v01@openssh.com` | query `rsa-sha2-512-cert-v01@openssh.com` |
| `rsa-sha2-256` | `OpenSSH_9.7` | | query `rsa-sha2-256-cert-v01@openssh.com` |
| `ssh-ed25519` (no RSA algorithm) | `OpenSSH_9.7` | query `ssh-rsa-cert-v01@openssh.com` | no request; `SSH public key authentication failed: No signing signature matched` |
| absent | `OpenSSH_9.7` | | query `ssh-rsa-cert-v01@openssh.com` |
| `ssh-ed25519,rsa-sha2-512` | `OpenSSH_7.7` | | query `ssh-rsa-cert-v01@openssh.com` |
| `ssh-ed25519,rsa-sha2-512` | `OpenSSH_7.8` | | query `rsa-sha2-512-cert-v01@openssh.com` |

The agent was not measured: libssh2 shares the method choice between a key file and an
agent identity, and the sign flags and the signature's plain method already follow the
chosen method (ADR-0271).

## Decision

- `SshUserAuthentication.ChooseSignatureAlgorithm` upgrades `ssh-rsa-cert-v01@openssh.com`
  as it upgrades `ssh-rsa` - the first of `rsa-sha2-512`, `rsa-sha2-256`, `ssh-rsa` the
  server names, `-cert-v01@openssh.com` appended, none (and the leftover method) when it
  names none - unless the preset's backend is WinCNG or the server's identification names
  OpenSSH before 7.8 (`OpenSshSignatureTypeBug`, read as libssh2 reads it).
- A preset that names no backend (`Full`) upgrades, as the OpenSSL build does.
- `SshTransport.ServerIdentification` exposes the banner the choice reads.

## Consequences

- An agent's RSA certificate on Linux and macOS asks the agent with flag 0 for an
  `rsa-sha2-*-cert-v01@openssh.com` method; an agent that answers with `ssh-rsa` is retried
  once from the certificate's own type, as ADR-0271 models.
- `SshUserAuthenticationTests` pin each measured row against the scripted peer.
