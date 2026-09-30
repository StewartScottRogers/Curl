---
id: BL-749
title: Verify OpenSSH host-key certificates and security-key host keys in the SSH transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-566, BL-678]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-749 — Verify OpenSSH host-key certificates and security-key host keys in the SSH transport

## Goal

The SSH transport accepts and verifies every host-key algorithm in ADR-0122's lists that BL-564 and BL-678 do not: the OpenSSH certificate forms (`rsa-sha2-512-cert-v01@openssh.com`, `rsa-sha2-256-cert-v01@openssh.com`, `ssh-rsa-cert-v01@openssh.com`, `ecdsa-sha2-nistp256/384/521-cert-v01@openssh.com`, `ssh-ed25519-cert-v01@openssh.com`, `sk-ecdsa-sha2-nistp256-cert-v01@openssh.com`, `sk-ssh-ed25519-cert-v01@openssh.com`) and the security-key forms (`sk-ecdsa-sha2-nistp256@openssh.com`, `sk-ssh-ed25519@openssh.com`), with the certificate's signature key checked against `@cert-authority` known-hosts entries as curl 8.21.0's libssh2 build treats them.

## Context

- ADR-0122: the certificate forms are in both reference builds' default host-key lists (measured 2026-09-28); the `sk-` forms come from libssh 0.12.2 and are in the full set only.
- Specifications: OpenSSH `PROTOCOL.certkeys` and `PROTOCOL.u2f` (security-key signatures carry a flags byte and counter after the signature; verification uses the BCL's `ECDsa` or BL-672's Ed25519 over SHA-256 of the application and the data).
- Builds on BL-566's known-hosts matcher.

## Acceptance criteria

- [x] Each certificate and `sk-` host-key algorithm verifies a signature over the exchange hash from ADR-0122's in-memory SSH peer, pinned by a test per algorithm.
- [x] A certificate whose signature fails, whose validity window has passed (by the injected `TimeProvider`) or whose principals do not name the host is refused with the exit code and message curl 8.21.0 gives, measured with `Record-CurlExchange.ps1` first.
- [x] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

- Measured first (2026-09-29, `Record-CurlExchange.ps1 -Script`, a KEXINIT offering one certificate name): curl 8.21.0 on Windows and curl 8.18.0 on libssh2 1.11.1/OpenSSL (WSL Ubuntu) both fail `-5, Unable to exchange encryption keys` (exit 2) for every RSA and ECDSA certificate name; only `ssh-ed25519-cert-v01@openssh.com` is agreed (OpenSSL list). libssh2 has no verifier for the others, and for the Ed25519 one reads only the nonce and certified key: no CA, CA signature, validity window or principals check; `@cert-authority` is not understood (ADR-0213).
- Decided in ADR-0266 (Claude, under delegation): every cert and `sk-` name gets a verifier; a certificate verifies H with its certified key and nothing else is checked, as curl does. So criterion 2 is answered by matching curl: an expired, wrong-principal or badly CA-signed certificate is not refused for that (pinned by `ExchangeKeysAsync_CertificateItselfUntrustworthy_StillVerifiesWithTheCertifiedKeyAsLibssh2Does`); with a known-hosts file a certificate is refused with exit 60 as before (`SshHostKeyCheckerTests`, measured row "a certificate type"). A certificate whose signature over H fails is refused with -8 like any bad signature (`ExchangeKeysAsync_BadSignature_FailsWithMinus8AsMeasured`, one row per name).
- `SshAlgorithmPreferences.HostKeysNeverAgreed` lists the names a reference preset offers but libssh2 never agrees; the negotiator passes over them. Curl's KEXINIT now offers each preset's host-key list in full, as the reference builds do.
- `sk-` signatures: PROTOCOL.u2f data SHA256(application) || flags || counter || SHA256(H); flags not checked (OpenSSH does not on a host key). libssh 0.12.2 not measured: no curl build here uses it.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0266 (no task in Doing names it). Also moved ADR-0265's index row, which had landed above the README heading, into the table.
- Coverage: every new class 100% line and branch (Cobertura, SSH test project).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Every OpenSSH certificate and sk- host-key algorithm verifies H; certificates are treated as libssh2 1.11.1 treats them (ADR-0266)
