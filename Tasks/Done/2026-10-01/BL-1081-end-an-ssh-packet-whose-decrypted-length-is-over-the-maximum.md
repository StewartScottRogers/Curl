---
id: BL-1081
title: End an SSH packet whose decrypted length is over the maximum with libssh2's -41 as both reference builds do
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1032]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1081 — End an SSH packet whose decrypted length is over the maximum with libssh2's -41 as both reference builds do

## Goal

An SSH packet whose decrypted `packet_length` is over the maximum ends with `curl: (2) Failure establishing ssh session: -41, Failed to get response to ssh-userauth request` (exit 2) where both reference builds print that, instead of today's -8 framing failure (ADR-0206).

## Context

- BL-1032 (2026-10-01, ADR-0259 "Measured 2026-10-01") flipped the top bit of the encrypted length of the server's `SERVICE_ACCEPT` under `chacha20-poly1305@openssh.com`: the OpenSSL build (`curlimages/curl:8.21.0`) and the Windows build (Git for Windows `mingw64\bin\curl.exe`, libssh2 1.11.1) both ended with exit 2 and `-41, Failed to get response to ssh-userauth request` (`LIBSSH2_ERROR_OUT_OF_BOUNDARY`).
- `SshPacketReader.RejectBadLength` (`Curl.Protocol.Ssh.UnitLibrary/Transport/SshPacketReader.cs`) throws one `InvalidDataException` for a zero length, an over-maximum length and one off the block size; ADR-0206 maps it to -8. libssh2 1.11.1's `fullpacket()` may distinguish them, so measure each (zero, over the maximum, off the block size) under chacha20-poly1305, AES-GCM and AES-CTR with HMAC, on both builds, with the harness BL-1032's Notes describe (a throwaway MSTest method bridging a `TcpListener` to `Fakes.InMemorySshServer`; run Docker containers detached and named and remove them afterwards, because killing `docker run` leaves its container running).
- Add `-41` to `Libssh2ErrorCode` if measured.

## Acceptance criteria

- [x] Each measured length case (code and stderr, per build and cipher) is copied into Notes and recorded in ADR-0206 or a new ADR.
- [x] `SshPacketReader` ends each case with the measured libssh2 code, pinned by `SshPacketReaderTests` and an `SshTransportTests` case for the stderr line.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Measured 2026-10-01 with a throwaway MSTest method (deleted) bridging a `TcpListener` to `Fakes.InMemorySshServer`, flipping bits of the encrypted `packet_length` of `SERVICE_ACCEPT` (28 under aes128-ctr + hmac-sha2-256, 32 under aes128-gcm@openssh.com, 24 under chacha20-poly1305@openssh.com); `-sS -k -m 15 -u u:p --ciphers <cipher> sftp://<host>:<port>/x`. Windows build: Git for Windows `mingw64\bin\curl.exe` (no AES-GCM). OpenSSL build: `docker run --rm --name bl1081 curlimages/curl:8.21.0` against host.docker.internal (Docker Desktop had to be started; no container was left running).
- Unaltered, both builds, every cipher: exit 78, `curl: (78) Could not open remote file for reading: No such file or directory` (harness works).
- Top bit set, both builds, every cipher: exit 2, `curl: (2) Failure establishing ssh session: -41, Failed to get response to ssh-userauth request`. Same for 40000 (both builds, every cipher) and for 39996, 40012 and 39980 (ctr). 39964 under ctr: exit 28 at `-m 15` (accepted, waiting for the rest). So libssh2 refuses 4 + packet_length + MAC > 40000 with -41.
- Zero, both builds, every cipher: exit 2, `curl: (2) Failure establishing ssh session: -12, Failed to get response to ssh-userauth request`. One early Windows ctr run printed `-8, Unable to exchange encryption keys`; three reruns gave -12.
- One off the block size: ctr aborts libssh2, `Assertion failed: (len % blocksize) == 0, file ../../libssh2-1.11.1/src/transport.c, line 139` (Windows exit 3; OpenSSL `(transport.c: decrypt: 139)`, exit 139); gcm and chacha wait for bytes and end at `-m 15`, exit 28.
- Decision (ADR-0206's new section, decided by Claude under Stewart's delegation): new `SshPacketLengthException` (not derived from `InvalidDataException`, which is sealed) with -12 for zero and -41 over the maximum, MAC or tag now counted; `ssh-userauth` answer prints the measured line; key exchange and re-exchange print the code with `Unable to exchange encryption keys` like a failed MAC (ADR-0212); before `KEXINIT` still -1; after auth still a connection failure (`SshConnectionFailure.Is`, `TryExchangeAsync`). Off the block size keeps today's codes (-43 at `ssh-userauth`, -8 in a key exchange): an assertion abort or a hang is not reproducible behaviour, as ADR-0259 decided.
- Correction to the Goal: today's code at the `ssh-userauth` answer was -43 (InvalidDataException), not -8; -8 was the re-exchange's.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. An SSH packet with a zero length ends with -12 and one over 40000 bytes (MAC included) with -41, as both reference builds measured
