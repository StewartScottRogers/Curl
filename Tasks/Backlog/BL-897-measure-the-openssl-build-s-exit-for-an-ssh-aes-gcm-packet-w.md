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
completed:
---
# BL-897 — Measure the OpenSSL build's exit for an SSH AES-GCM packet whose tag fails

## Goal

`Libssh2ErrorCode.Decrypt` (-12), which `AesGcmPacketProtection` reports when a packet's tag fails, is confirmed or corrected against the OpenSSL reference build of curl 8.21.0, and ADR-0212 says which.

## Context

- BL-565 measured the Windows (WinCNG) build: a corrupted MAC under `aes*-ctr` with `hmac-sha2-256` or `hmac-sha2-512-etm@openssh.com` ends curl with exit 2 and `Failure establishing ssh session: -4, Failed to get response to ssh-userauth request`. The Windows build offers no AES-GCM, so the GCM code was taken from libssh2 1.11.1's `transport.c` (`decrypt()` returns `LIBSSH2_ERROR_DECRYPT` when the cipher refuses): -12, unmeasured.
- The measurement needs a server that completes a real key exchange with curl and then sends a packet with a flipped tag byte. BL-565 did this with a throwaway MSTest method in `Curl.Protocol.Ssh.UnitTests` that played the server over a `TcpListener` from the library's own classes (group14-sha256, `rsa-sha2-256` from `TestHostKey`, `SshServerScript.Protect`), and ran curl against it; the Windows curl worked, but WSL's Ubuntu curl (8.18.0, OpenSSL, libssh2 1.11.1) could not reach the Windows listener at the host's WSL address (the connection never arrived) and Docker was down. Either run the listener where the OpenSSL curl can reach it (the `curlimages/curl:8.21.0` image with Docker up, or WSL with a firewall rule for the listener's port), or extend `Record-CurlExchange.ps1` with an SSH mode.
- ADR-0212 records the decision this confirms.

## Acceptance criteria

- [ ] The OpenSSL build's exit code and stderr for an `aes256-gcm@openssh.com` packet with one tag byte flipped, and for one ciphertext byte flipped, are copied into Notes.
- [ ] If they differ from `-12`, `Libssh2ErrorCode.Decrypt` and the `Curl.Protocol.Ssh.UnitTests` that pin it are corrected; either way ADR-0212 records the measurement.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-29: Created.
