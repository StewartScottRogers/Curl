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
completed: 2026-10-01
---
# BL-1032 — Measure what the OpenSSL build does with a chacha20-poly1305 SSH packet whose tag fails

## Goal

`ChaCha20Poly1305PacketProtection`'s failure for a packet whose Poly1305 tag fails (today `Libssh2ErrorCode.Decrypt`, -12, from ADR-0259) matches what the OpenSSL reference build of curl 8.21.0 does, and ADR-0259 records the measurement.

## Context

- BL-897 (2026-09-30) ran `curlimages/curl:8.21.0` (OpenSSL 3.5.7, libssh2 1.11.1) under Docker Desktop, `curl -sS -k -u u:p sftp://host.docker.internal:<port>/x`, against a throwaway MSTest method: a `TcpListener` bridged to `Fakes.InMemorySshServer` with `Cipher = "chacha20-poly1305@openssh.com"`, the server-to-client pump flipping one byte of the first packet after `NEWKEYS` (`SERVICE_ACCEPT`). Unaltered, curl ended with exit 78 (`Could not open remote file for reading: No such file or directory`), so the cipher interoperates. With the last tag byte flipped, or ciphertext byte 8 flipped, curl printed nothing and never exited on its own: the container ended with 137 (killed) after minutes. AES-GCM under the same harness ended at once with exit 2 and `-12, Failed to get response to ssh-userauth request`.
- So the OpenSSL build appears to hang (or wait for more bytes) on a bad chacha20-poly1305 tag rather than fail with -12. Find out which: time how long curl waits, whether `--connect-timeout`/`-m` ends it and with what exit and stderr, whether it reads further bytes (libssh2 1.11.1 `transport.c` `fullpacket()` and `crypt.c`'s chacha path), and whether the pump's byte-8 flip hit the encrypted length (the length is at bytes 0-3 and encrypted under chacha).
- Start from the harness described in BL-897's Notes; delete the throwaway method after the run.

## Acceptance criteria

- [x] The OpenSSL build's behaviour (exit code, stderr, and whether and after how long it exits) for a `chacha20-poly1305@openssh.com` packet with one tag byte flipped, and with one payload ciphertext byte flipped, is copied into Notes.
- [x] `ChaCha20Poly1305PacketProtection` (or the reader above it) and the `Curl.Protocol.Ssh.UnitTests` that pin its failure behave as measured, and ADR-0259 records the measurement.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-10-01. Harness: a throwaway MSTest method (deleted) bridging a `TcpListener` on `IPAddress.Any` to `Fakes.InMemorySshServer` with `Cipher = "chacha20-poly1305@openssh.com"`, the server-to-client pump altering one byte of the first chunk after the 16-byte `NEWKEYS` packet (`SERVICE_ACCEPT`, sequence number 3). OpenSSL build: `docker run -d --name bl1032-<n> curlimages/curl:8.21.0 -sS -k -u u:p sftp://host.docker.internal:<port>/x`, polled with `docker inspect` and `docker stats`. Windows build: Git for Windows `mingw64\bin\curl.exe` (libssh2/1.11.1), the same arguments against 127.0.0.1. Lesson: killing a `docker run` client leaves the container running, so the first two runs' "killed at the limit" containers kept spinning; run detached and named, then `docker rm -f`.
- OpenSSL build, unaltered: exit 78, `curl: (78) Could not open remote file for reading: No such file or directory` (harness works).
- OpenSSL build, last tag byte flipped: no stdout, no stderr, never exits on its own. One core at 100%, memory growing ~160 MiB/s (4.0 GiB at 24 s, 30 GiB at 208 s); the kernel OOM-kills it (`OOMKilled=true`, exit 137) after 4.5 to 10 minutes. curl sends no further byte after the bad packet.
- OpenSSL build, ciphertext byte 8 flipped (byte 8 is inside the encrypted payload; bytes 0-3 are the length): identical (OOM-killed, exit 137, 60 GiB at 461 s).
- OpenSSL build, tag flipped with `-m 10`, or with `--connect-timeout 5`: identical; neither ends it. With `-v` the last stderr line is `* SSH: user 'u'`. With the server closing the TCP connection 20 s after the bad packet: identical; the close does not end it. So libssh2 1.11.1 loops inside one blocking read of the packet, allocating, and curl's timeouts never get control.
- Windows build: unaltered exit 78 (same line); tag or ciphertext byte 8 flipped: no output, 100% of a core, working set +~240 MiB/s (4.3 GiB at 18 s, when the harness killed it). Same defect.
- Both builds, top bit of the encrypted length flipped (length over the maximum): exit 2 at once, `curl: (2) Failure establishing ssh session: -41, Failed to get response to ssh-userauth request`. Curl gives -8 there today (ADR-0206); filed BL-1078.
- Decision (ADR-0259, decided by Claude under Stewart's delegation): Curl keeps ending a failed chacha20-poly1305 tag with -12, exit 2, `Failed to get response to ssh-userauth request`, as both builds do for AES-GCM. Reproducing an unkillable, memory-exhausting loop is a libssh2 defect no script can rely on and would take the user's machine down. So `ChaCha20Poly1305PacketProtection` and its tests (`Open_AlteredPacket_ThrowsWithMinus12`, `SshPacketReaderTests` -12 row, `SshTransportTests.KeyExchange` "ChaCha20-Poly1305 tag" row) are unchanged; only `Libssh2ErrorCode.Decrypt`'s doc comment and ADR-0259 (new "Measured 2026-10-01" section) changed.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Both reference builds measured: a failed chacha20-poly1305 tag loops until OOM-killed; Curl keeps -12 by decision, recorded in ADR-0259
