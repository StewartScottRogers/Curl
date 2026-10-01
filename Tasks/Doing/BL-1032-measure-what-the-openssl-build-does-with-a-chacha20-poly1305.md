---
id: BL-1032
title: Measure what the OpenSSL build does with a chacha20-poly1305 SSH packet whose tag fails
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-897]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-30
completed:
---
# BL-1032 — Measure what the OpenSSL build does with a chacha20-poly1305 SSH packet whose tag fails

## Goal

`ChaCha20Poly1305PacketProtection`'s failure for a packet whose Poly1305 tag fails (today `Libssh2ErrorCode.Decrypt`, -12, from ADR-0259) matches what the OpenSSL reference build of curl 8.21.0 does, and ADR-0259 records the measurement.

## Context

- BL-897 (2026-09-30) ran `curlimages/curl:8.21.0` (OpenSSL 3.5.7, libssh2 1.11.1) under Docker Desktop, `curl -sS -k -u u:p sftp://host.docker.internal:<port>/x`, against a throwaway MSTest method: a `TcpListener` bridged to `Fakes.InMemorySshServer` with `Cipher = "chacha20-poly1305@openssh.com"`, the server-to-client pump flipping one byte of the first packet after `NEWKEYS` (`SERVICE_ACCEPT`). Unaltered, curl ended with exit 78 (`Could not open remote file for reading: No such file or directory`), so the cipher interoperates. With the last tag byte flipped, or ciphertext byte 8 flipped, curl printed nothing and never exited on its own: the container ended with 137 (killed) after minutes. AES-GCM under the same harness ended at once with exit 2 and `-12, Failed to get response to ssh-userauth request`.
- So the OpenSSL build appears to hang (or wait for more bytes) on a bad chacha20-poly1305 tag rather than fail with -12. Find out which: time how long curl waits, whether `--connect-timeout`/`-m` ends it and with what exit and stderr, whether it reads further bytes (libssh2 1.11.1 `transport.c` `fullpacket()` and `crypt.c`'s chacha path), and whether the pump's byte-8 flip hit the encrypted length (the length is at bytes 0-3 and encrypted under chacha).
- Start from the harness described in BL-897's Notes; delete the throwaway method after the run.

## Acceptance criteria

- [ ] The OpenSSL build's behaviour (exit code, stderr, and whether and after how long it exits) for a `chacha20-poly1305@openssh.com` packet with one tag byte flipped, and with one payload ciphertext byte flipped, is copied into Notes.
- [ ] `ChaCha20Poly1305PacketProtection` (or the reader above it) and the `Curl.Protocol.Ssh.UnitTests` that pin its failure behave as measured, and ADR-0259 records the measurement.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
