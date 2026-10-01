---
id: BL-1097
title: Sign with a --key private key whose --pubkey is an OpenSSH certificate, as libssh2 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-1036]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1097 — Sign with a --key private key whose --pubkey is an OpenSSH certificate, as libssh2 does

## Goal

`curl --key id_rsa --pubkey id_rsa-cert.pub sftp://...` (and the ECDSA and Ed25519 certificate forms) authenticates with the certificate as curl's libssh2 1.11.1 does, instead of failing the method because the private key's type is not the certificate's (`SshInfoLines.SignCallbackFailed`).

## Context

`SshUserAuthentication.SendSignedPublicKeyAsync` fails the method when the private key's `KeyType` differs from the public key's, and a certificate's type (`ssh-rsa-cert-v01@openssh.com`, ...) never equals its private key's. libssh2's `libssh2_userauth_publickey_fromfile_ex` signs with the private key file under the chosen method and sends the certificate blob; the signature names `plain_method`'s form. BL-1036 (ADR-0311) already chooses the upgraded certificate method on OpenSSL. Found in BL-1036. Measure first with the reference curl (Windows) and the WSL Ubuntu curl (libssh2 1.11.1, OpenSSL) against a loopback server that accepts the certificate, as ADR-0311 did, including a certificate whose key is not the private key's.

## Acceptance criteria

- [x] Measured first, in Notes: the request, signature method and `-v` lines for an RSA and an Ed25519 certificate on both builds, and for a certificate whose key does not match `--key`.
- [x] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 (ADR-0312 has the table) against a real OpenSSH 10.2p1 sshd: the
  Ubuntu `openssh-server`, `openssh-sftp-server` and `libwrap0` packages fetched from
  archive.ubuntu.com, extracted with `dpkg -x` into `/tmp/bl1097` in WSL and run
  unprivileged on 127.0.0.1:2299 (`LD_LIBRARY_PATH` for libwrap, `SshdSessionPath`,
  `SshdAuthPath`, `UsePAM no`, `TrustedUserCAKeys` an `ssh-keygen` Ed25519 CA, RSA host key
  and `diffie-hellman-group14-sha256` added for WinCNG, `PubkeyAcceptedAlgorithms
  +ssh-rsa-cert-v01@openssh.com`). Nothing committed. This beats a throwaway in-memory server
  because sshd really verifies the certificate and the signature.
  `curl -sS -v -k -u stewart_rogers: --key K --pubkey C sftp://127.0.0.1:2299/tmp/bl1097/x.txt`:
  - Ubuntu curl 8.18.0 (OpenSSL): RSA cert -> query `rsa-sha2-512-cert-v01@openssh.com`,
    `Authentication complete`; Ed25519 cert -> `ssh-ed25519-cert-v01@openssh.com`, complete;
    ECDSA P-256 cert -> `ecdsa-sha2-nistp256-cert-v01@openssh.com`, complete; `--key
    rsa_other` with the RSA cert -> signed, `SSH public key authentication failed: Invalid
    signature for supplied public key, or bad username/public key combination` (67);
    `--key ed` with the RSA cert -> `Callback returned error` (67).
  - Windows curl 8.21.0 (WinCNG): RSA cert -> query `ssh-rsa-cert-v01@openssh.com`,
    `SSH: authenticated via publickey` (with OpenSSH 10's default `PubkeyAcceptedAlgorithms`
    the server refuses that method at the question: `Username/PublicKey combination
    invalid`); other RSA key -> `Invalid signature ...`; Ed25519 key -> `Callback returned
    error`; Ed25519 and ECDSA certs -> `Callback returned error` - and so do plain Ed25519 and
    ECDSA keys (WinCNG's libssh2 cannot read them), filed as BL-1098, not widened here.
  - sshd accepted each signature, so each was in the certificate's plain form (sshd checks
    the signature's algorithm against the certificate's).
- Change: `SendSignedPublicKeyAsync` compares the private key with the public key's plain
  type (`PlainMethods`) and signs under the chosen method's plain form (ADR-0312).
- Tests: `SshUserAuthenticationTests.KeyFileCertificate.cs` - four signing rows (RSA on
  OpenSSL and WinCNG, Ed25519, ECDSA), the refused other-key case and the other-type case.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A --pubkey OpenSSH certificate (RSA, Ed25519, ECDSA) signs with its --key private key under the plain method as libssh2 1.11.1 does, measured against a real sshd on both builds (ADR-0312)
