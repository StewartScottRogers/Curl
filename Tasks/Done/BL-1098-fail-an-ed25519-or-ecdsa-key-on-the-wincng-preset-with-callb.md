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
completed: 2026-10-01
---
# BL-1098 — Fail an Ed25519 or ECDSA --key on the WinCNG preset with Callback returned error, as Windows curl does

## Goal

On the WinCNG preset (`SshAlgorithmPreferences.WindowsReference`), `curl --key ed --pubkey ed.pub sftp://...` with an Ed25519 or ECDSA private key asks the question, then fails the `publickey` method with `* SSH: publickey authentication denied: Callback returned error` and sends no signed request, as curl 8.21.0 (libssh2 1.11.1, WinCNG) does; the OpenSSL preset keeps signing.

## Context

Found in BL-1097. Measured 2026-10-01 against OpenSSH 10.2's sshd (extracted from the Ubuntu `openssh-server` package into WSL, run as the user on a loopback port with the keys in `AuthorizedKeysFile`): Windows curl 8.21.0 with `--key ed --pubkey ed.pub` and with `--key ec --pubkey ec.pub` (ECDSA P-256) reports `Callback returned error` after the server's PK_OK; Ubuntu curl 8.18.0 (OpenSSL) authenticates. WinCNG's libssh2 cannot read an Ed25519 or ECDSA private key file, so its sign callback fails. The same holds for their certificates (`ssh-ed25519-cert-v01@openssh.com`, `ecdsa-sha2-nistp256-cert-v01@openssh.com`). `SshUserAuthentication.SendSignedPublicKeyAsync` is where the private key is read; `transport.CryptographyBackend` names the preset's backend. Without `--pubkey`, check what WinCNG's libssh2 reports when it derives the public key from such a private key (likely the `Reason unknown` path of BL-990) and measure before pinning.

## Acceptance criteria

- [x] Measured first, in Notes: Windows and Ubuntu curl `-v` lines for an Ed25519 and an ECDSA P-256 key, with and without `--pubkey`.
- [x] `Curl.Protocol.Ssh.UnitTests` pin each measured case against the in-memory peer; the OpenSSL and `Full` presets still sign.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01 (ADR-0314 has the table) against OpenSSH 10.2p1's sshd: the Ubuntu
  `openssh-server` and `openssh-sftp-server` 10.2p1-2ubuntu3.7 packages fetched from
  archive.ubuntu.com, `dpkg -x` into `/tmp/bl1098` in WSL, run unprivileged on
  127.0.0.1:2298 (`UsePAM no`, RSA host key, `diffie-hellman-group14-sha256` added for
  WinCNG, `PerSourcePenalties no` so repeated failures are not dropped). Nothing committed.
  `curl -sS -v -k -u stewart_rogers: --key K [--pubkey K.pub] sftp://127.0.0.1:2298/tmp/bl1098/x.txt`:
  - Windows curl 8.21.0 (WinCNG), `--key ed --pubkey ed.pub` and `--key ec --pubkey ec.pub`
    (P-256, `openssh-key-v1`, SEC 1 PEM and PKCS #8 alike): `* SSH: trying public key file
    'K.pub'`, `* SSH: trying private key file 'K'`, `* SSH: publickey authentication denied:
    Callback returned error`, `* SSH: trying publickey authentication via agent`,
    `* SSH: failure connecting to agent`, `curl: (67) Login denied`.
  - Windows, no `--pubkey`: `* SSH: trying private key file 'K'`, `* SSH: publickey
    authentication denied: Reason unknown (-1)`, then the same agent lines, 67.
  - Ubuntu curl 8.18.0 (OpenSSL): Ed25519 and ECDSA authenticate with and without
    `--pubkey`, measured on the same sshd (8.18.0 writes none of 8.21.0's `SSH:` lines).
  - Also seen: Windows curl reads RSA only from PKCS #1 PEM; `openssh-key-v1` and PKCS #8
    RSA fail the same two ways. Left unmatched, as ADR-0122/ADR-0230 decided (ADR-0314 says
    why: a missing algorithm is matched, a narrower parser is not). Not filed.
- Change: `SshUserAuthentication.BackendReadsPrivateKey` - on the WinCNG backend an Ed25519
  or ECDSA key is not read: a derived public key counts as underivable (BL-990's `Reason
  unknown` path) and a `--pubkey` question that gets `PK_OK` fails with `Callback returned
  error`. `CanSign` keeps `SendSignedPublicKeyAsync` at complexity 10.
- Tests: `SshUserAuthenticationTests.WinCngKeyTypes.cs` - the `--pubkey` and no-`--pubkey`
  WinCNG cases per key format, and OpenSSL, `Full` and PKCS #1 RSA on WinCNG still signing.
  Ssh tests 1571 passed; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary`:
  100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. On the WinCNG preset an Ed25519 or ECDSA --key fails with Callback returned error after PK_OK (with --pubkey) or Reason unknown (-1) (without), as Windows curl 8.21.0 does; OpenSSL and Full still sign (ADR-0314)
