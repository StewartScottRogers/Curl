---
id: BL-990
title: Report the OpenSSL build's publickey denial reason for an unreadable SSH key off Windows
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-681]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-990 — Report the OpenSSL build's publickey denial reason for an unreadable SSH key off Windows

## Goal

On Linux and macOS, `-v` on an `scp`/`sftp` transfer whose `--key` cannot be read (missing passphrase, wrong passphrase, `chacha20-poly1305@openssh.com` key, unknown format) names libssh2's OpenSSL-backend reason instead of the WinCNG build's `Reason unknown (-1)`.

## Context

- Found by BL-681. With no `--pubkey`, libssh2 1.11.1 derives the public key from the private key file; the reason it gives when that fails depends on the cryptography backend.
- Windows reference (curl 8.21.0, libssh2 1.11.1 WinCNG, measured by BL-568): `SSH: publickey authentication denied: Reason unknown (-1)`, exit 67 `Authentication failure`.
- Ubuntu curl 8.18.0 on libssh2 1.11.1 OpenSSL 3.5.5, measured by BL-681 against OpenSSH 10.2 (`sftp`, `-u user:wrong`, `--key` an encrypted Ed25519 key with `--pass nope`, no `--pass`, or a `chacha20-poly1305@openssh.com` key): curl 8.18's wording `SSH public key authentication failed: Unable to extract public key from private key file: Wrong passphrase or invalid/unrecognized private key file format`, then exit 67 `Authentication failure`. curl 8.21.0's `lib/vssh/libssh2.c` writes the same libssh2 message after `SSH: publickey authentication denied: ` (ADR-0262).
- `SshAlgorithmPreferences.CryptographyBackend` already tells the two builds apart; `SshInfoLines.ReasonUnknown` is the line to make per backend.

## Acceptance criteria

- [ ] Measured with the OpenSSL build of curl 8.21.0 (or its `lib/vssh/libssh2.c` read, if no 8.21.0 OpenSSL build is available) and copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the OpenSSL-backend reason for the OpenSSL preset and keep `Reason unknown (-1)` for the WinCNG preset.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
