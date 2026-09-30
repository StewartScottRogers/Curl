---
id: BL-897
title: Measure the OpenSSL build's exit for an SSH AES-GCM packet whose tag fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-565]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-897 — Measure the OpenSSL build's exit for an SSH AES-GCM packet whose tag fails

## Goal

`Libssh2ErrorCode.Decrypt` (-12), which `AesGcmPacketProtection` reports when a packet's tag fails, is confirmed or corrected against the OpenSSL reference build of curl 8.21.0, and ADR-0212 says which.

## Context

- BL-565 measured the Windows (WinCNG) build: a corrupted MAC under `aes*-ctr` with `hmac-sha2-256` or `hmac-sha2-512-etm@openssh.com` ends curl with exit 2 and `Failure establishing ssh session: -4, Failed to get response to ssh-userauth request`. The Windows build offers no AES-GCM, so the GCM code was taken from libssh2 1.11.1's `transport.c` (`decrypt()` returns `LIBSSH2_ERROR_DECRYPT` when the cipher refuses): -12, unmeasured.
- The measurement needs a server that completes a real key exchange with curl and then sends a packet with a flipped tag byte. BL-565 did this with a throwaway MSTest method in `Curl.Protocol.Ssh.UnitTests` that played the server over a `TcpListener` from the library's own classes (group14-sha256, `rsa-sha2-256` from `TestHostKey`, `SshServerScript.Protect`), and ran curl against it; the Windows curl worked, but WSL's Ubuntu curl (8.18.0, OpenSSL, libssh2 1.11.1) could not reach the Windows listener at the host's WSL address (the connection never arrived) and Docker was down. Either run the listener where the OpenSSL curl can reach it (the `curlimages/curl:8.21.0` image with Docker up, or WSL with a firewall rule for the listener's port), or extend `Record-CurlExchange.ps1` with an SSH mode.
- ADR-0212 records the decision this confirms.

## Acceptance criteria

- [x] The OpenSSL build's exit code and stderr for an `aes256-gcm@openssh.com` packet with one tag byte flipped, and for one ciphertext byte flipped, are copied into Notes.
- [x] If they differ from `-12`, `Libssh2ErrorCode.Decrypt` and the `Curl.Protocol.Ssh.UnitTests` that pin it are corrected; either way ADR-0212 records the measurement.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-09-30 with `curlimages/curl:8.21.0` (curl 8.21.0, OpenSSL/3.5.7, libssh2/1.11.1, musl) under Docker Desktop: `docker run --rm curlimages/curl:8.21.0 -sS -k -u u:p sftp://host.docker.internal:<port>/x`. The server was a throwaway MSTest method (deleted after the run): a `TcpListener` bridged to `Fakes.InMemorySshServer` with `Cipher` set, the server-to-client pump passing the plain packets through and altering one byte of the first packet after `NEWKEYS` (`SERVICE_ACCEPT`): the last byte (tag) or byte 8 (ciphertext).
- `aes256-gcm@openssh.com`, tag byte flipped: exit 2, stderr `curl: (2) Failure establishing ssh session: -12, Failed to get response to ssh-userauth request`.
- `aes256-gcm@openssh.com`, ciphertext byte flipped: exit 2, stderr `curl: (2) Failure establishing ssh session: -12, Failed to get response to ssh-userauth request`.
- `aes128-gcm@openssh.com`, tag or ciphertext flipped: the same exit 2 and -12 line. Unaltered `aes256-gcm@openssh.com`: exit 78, `curl: (78) Could not open remote file for reading: No such file or directory` (the harness works). `aes128-ctr` + `hmac-sha2-256`, MAC flipped: exit 2 and -4, as on Windows.
- Outcome: -12 confirmed; `Libssh2ErrorCode.Decrypt` and its tests unchanged, only its doc comment (no longer "not yet measured"). ADR-0212 gained a "Measured 2026-09-30" table; ADR-0259 points at the finding below.
- Found: `chacha20-poly1305@openssh.com` interoperates unaltered (exit 78), but with its tag or a ciphertext byte altered curl printed nothing and never exited on its own (container exit 137 after minutes). Out of this task's scope (AES-GCM); filed BL-1032 to measure it.
- Docker Desktop was up, so the WSL route BL-565 tried was not needed; host.docker.internal reaches a listener on IPAddress.Any.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. OpenSSL curl 8.21.0 measured: a failed AES-GCM tag or ciphertext ends with exit 2 and -12, confirming Libssh2ErrorCode.Decrypt; ADR-0212 records it
