---
id: BL-1098
title: Fail an Ed25519 or ECDSA --key on the WinCNG preset with Callback returned error, as Windows curl does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1098 — Fail an Ed25519 or ECDSA --key on the WinCNG preset with Callback returned error, as Windows curl does

## Goal

On the WinCNG preset (`SshAlgorithmPreferences.WindowsReference`), `curl --key ed --pubkey ed.pub sftp://...` with an Ed25519 or ECDSA private key asks the question, then fails the `publickey` method with `* SSH: publickey authentication denied: Callback returned error` and sends no signed request, as curl 8.21.0 (libssh2 1.11.1, WinCNG) does; the OpenSSL preset keeps signing.

## Context

Found in BL-1097. Measured 2026-10-01 against OpenSSH 10.2's sshd (extracted from the Ubuntu `openssh-server` package into WSL, run as the user on a loopback port with the keys in `AuthorizedKeysFile`): Windows curl 8.21.0 with `--key ed --pubkey ed.pub` and with `--key ec --pubkey ec.pub` (ECDSA P-256) reports `Callback returned error` after the server's PK_OK; Ubuntu curl 8.18.0 (OpenSSL) authenticates. WinCNG's libssh2 cannot read an Ed25519 or ECDSA private key file, so its sign callback fails. The same holds for their certificates (`ssh-ed25519-cert-v01@openssh.com`, `ecdsa-sha2-nistp256-cert-v01@openssh.com`). `SshUserAuthentication.SendSignedPublicKeyAsync` is where the private key is read; `transport.CryptographyBackend` names the preset's backend. Without `--pubkey`, check what WinCNG's libssh2 reports when it derives the public key from such a private key (likely the `Reason unknown` path of BL-990) and measure before pinning.

## Acceptance criteria

- [ ] Measured first, in Notes: Windows and Ubuntu curl `-v` lines for an Ed25519 and an ECDSA P-256 key, with and without `--pubkey`.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer; the OpenSSL and `Full` presets still sign.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
