# ADR-0266 — SSH certificate host keys verify H with the certified key, and the reference presets agree only the Ed25519 one

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-749.

## Context

ADR-0122's host-key table lists ten names BL-749 implements: the OpenSSH certificate forms
`ecdsa-sha2-nistp256/384/521-cert-v01@openssh.com`, `rsa-sha2-512-cert-v01@openssh.com`,
`rsa-sha2-256-cert-v01@openssh.com`, `ssh-rsa-cert-v01@openssh.com` and
`ssh-ed25519-cert-v01@openssh.com` (both reference builds offer them), and the libssh-only
security-key forms `sk-ecdsa-sha2-nistp256@openssh.com`, `sk-ssh-ed25519@openssh.com` and
their `-cert-v01` forms (`Full` only). The task asked for certificates to be checked
against `@cert-authority` known-hosts entries, their validity window and their principals
"as curl 8.21.0's libssh2 build treats them", measured first.

### Measured 2026-09-29

`Record-CurlExchange.ps1 -Script` served a server identification and a `KEXINIT` whose
only host-key algorithm was one certificate name (key exchange
`diffie-hellman-group14-sha256`, `aes128-ctr`, `hmac-sha2-256`), then recorded what curl
sent next. Run with `-sS -k -u u:p sftp://...` against the Windows reference build (curl
8.21.0, libssh2 1.11.1 on WinCNG) and, over the WSL network, Ubuntu's curl 8.18.0 on
libssh2 1.11.1 and OpenSSL (the same libssh2 as the OpenSSL reference build):

| Server's only host key | Windows | OpenSSL |
| --- | --- | --- |
| `rsa-sha2-512-cert-v01`, `rsa-sha2-256-cert-v01`, `ssh-rsa-cert-v01` | exit 2, `Failure establishing ssh session: -5, Unable to exchange encryption keys`; curl sends `DISCONNECT` 11 and no `KEXDH_INIT` | the same |
| `ecdsa-sha2-nistp256/384/521-cert-v01` | not offered | the same -5 failure |
| `ssh-ed25519-cert-v01` | not offered | agreed: curl sends `KEXDH_INIT` |
| `rsa-sha2-256`, `ssh-ed25519` (controls) | agreed | agreed |

That matches libssh2 1.11.1's source: its RSA and ECDSA certificate methods have no
`init` or `sig_verify` (they exist for signing with a user certificate), and
`kex_agree_hostkey` passes over a method that cannot verify, so they are offered but never
agreed; its Ed25519 `init` skips a certificate's nonce and reads the certified key, so
`ssh-ed25519-cert-v01` is agreed and the signature over H is checked with that key. Nothing
else in the certificate is read: not the CA key, not its signature, not the validity
window, not the principals. After the handshake `libssh2_session_hostkey` reports a
certificate as `LIBSSH2_HOSTKEY_TYPE_UNKNOWN`, so curl's known-hosts check prints
`SSH: unsupported host key type for knownhosts check` and refuses it (exit 60, ADR-0213), and
libssh2's known-hosts reader does not understand `@cert-authority` at all (ADR-0213).

## Decision

- Every one of the ten names has a verifier in `SshSignatureVerifiers`, so the catalogue
  offers each preset's host-key list in full and Curl's `KEXINIT` is byte for byte the
  reference build's again.
- `OpenSshCertificateSshSignatureVerifier` reads a certificate as libssh2 reads the
  Ed25519 one: the name, the nonce and the certified key's fields, which it rebuilds into
  the plain key blob and hands to that key's own verifier. The rest of the certificate is
  not read. Curl does not check the CA, its signature, the validity window or the
  principals, because curl does not: a certificate is accepted or refused exactly as curl
  accepts or refuses it - under `-k` or a matching `--hostpubsha256`/`--hostpubmd5`
  accepted, with a known-hosts file refused with exit 60 (`SshHostKeyChecker`, unchanged).
  The task's "expired, wrong principals or bad CA signature is refused" criterion is
  answered by this: no build of curl 8.21.0 on libssh2 refuses for those reasons, so
  neither does Curl, and a test pins that such a certificate still verifies H.
- `SshAlgorithmPreferences.HostKeysNeverAgreed` lists what a preset offers but never
  agrees - the RSA certificate names on Windows, the RSA and ECDSA ones on OpenSSL, none
  in `Full` - and `SshAlgorithmNegotiator` passes over them, so a server that shares only
  those fails with -5 as measured, and one that also shares a plain key agrees that key.
- `SecurityKeyEcdsaSshSignatureVerifier` and `SecurityKeyEd25519SshSignatureVerifier`
  verify OpenSSH's `PROTOCOL.u2f` signatures: ECDSA P-256 over SHA-256, or Ed25519, of
  SHA256(application) || flags || counter || SHA256(H), with the flags byte and counter
  read after the signature. The flags are not checked: OpenSSH checks user presence only
  for user authentication, never on the host key's signature over H. The `sk-` names are
  libssh's and run only under `Full`; libssh 0.12.2 was not measured, since no curl build
  on this machine uses it.

## Consequences

- `Full` agrees and verifies every host-key name ADR-0122 lists; the reference presets
  agree the same names their libssh2 does.
- `@cert-authority` entries remain entries for a host named `@cert-authority`
  (ADR-0213). Checking certificates against a CA would make Curl accept hosts real curl
  refuses, so it waits until a reference build does it.
