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
completed:
---
# BL-1081 — End an SSH packet whose decrypted length is over the maximum with libssh2's -41 as both reference builds do

## Goal

An SSH packet whose decrypted `packet_length` is over the maximum ends with `curl: (2) Failure establishing ssh session: -41, Failed to get response to ssh-userauth request` (exit 2) where both reference builds print that, instead of today's -8 framing failure (ADR-0206).

## Context

- BL-1032 (2026-10-01, ADR-0259 "Measured 2026-10-01") flipped the top bit of the encrypted length of the server's `SERVICE_ACCEPT` under `chacha20-poly1305@openssh.com`: the OpenSSL build (`curlimages/curl:8.21.0`) and the Windows build (Git for Windows `mingw64\bin\curl.exe`, libssh2 1.11.1) both ended with exit 2 and `-41, Failed to get response to ssh-userauth request` (`LIBSSH2_ERROR_OUT_OF_BOUNDARY`).
- `SshPacketReader.RejectBadLength` (`Curl.Protocol.Ssh.UnitLibrary/Transport/SshPacketReader.cs`) throws one `InvalidDataException` for a zero length, an over-maximum length and one off the block size; ADR-0206 maps it to -8. libssh2 1.11.1's `fullpacket()` may distinguish them, so measure each (zero, over the maximum, off the block size) under chacha20-poly1305, AES-GCM and AES-CTR with HMAC, on both builds, with the harness BL-1032's Notes describe (a throwaway MSTest method bridging a `TcpListener` to `Fakes.InMemorySshServer`; run Docker containers detached and named and remove them afterwards, because killing `docker run` leaves its container running).
- Add `-41` to `Libssh2ErrorCode` if measured.

## Acceptance criteria

- [ ] Each measured length case (code and stderr, per build and cipher) is copied into Notes and recorded in ADR-0206 or a new ADR.
- [ ] `SshPacketReader` ends each case with the measured libssh2 code, pinned by `SshPacketReaderTests` and an `SshTransportTests` case for the stderr line.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-01: Created.
