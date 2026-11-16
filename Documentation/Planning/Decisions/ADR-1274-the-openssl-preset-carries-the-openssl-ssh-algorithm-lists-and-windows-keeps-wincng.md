# ADR-1274: The OpenSSL preset carries the OpenSSL SSH algorithm lists; Windows keeps the WinCNG preset

- Status: Accepted
- Date: 2026-11-16
- Decided by Claude under Stewart's delegation (BL-5283)

## Context
Surl measured the port on Windows beside curl 8.21.0's OpenSSL build: that build's `KEXINIT` lists Curve25519, ECDH, ECDSA, Ed25519, AES-GCM, Blowfish, CAST-128 and RIPEMD-160, and the port sends the WinCNG lists.

## Decision
`SshAlgorithmPreferences.OpenSslReference` already holds libssh2 1.11.1's OpenSSL lists in order and the packet layer implements Blowfish, CAST-128 and RIPEMD-160; `SshAlgorithmPreferencesOpenSslTests` pins both. `WindowsReference` stays the Windows Schannel/WinCNG build's list, because the port matches the platform's curl. Choosing the OpenSSL preset on Windows is a build-selection change in the engine composition, outside this task's files, and is not made.

## Consequences
Linux and macOS behave as the OpenSSL build. On Windows a server offering only a weak algorithm still fails with exit 2, as the Windows curl does.
