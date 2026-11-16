# ADR-1273: ssh-ed25519 is offered by the OpenSSL preset and not the WinCNG preset

- Status: Accepted
- Date: 2026-11-16
- Decided by Claude under Stewart's delegation (BL-5279)

## Context
Surl measured the port against upstream curl 8.21.0's OpenSSL build on Windows, which offers `ssh-ed25519`; the port on Windows sends the WinCNG list (RSA only).

## Decision
`SshAlgorithmPreferences.OpenSslReference` already offers `ssh-ed25519` and keeps it agreeable; a unit test pins that. `WindowsReference` stays byte for byte the Windows Schannel/WinCNG build's list (libssh2 on WinCNG has no Ed25519), because the port matches the platform's curl. Picking the OpenSSL preset on Windows is a build-selection change outside this task's files and is not made here.

## Consequences
On Linux and macOS the port already behaves as the OpenSSL build. A Windows run against an Ed25519-only server still exits 2, as the Windows curl does.
