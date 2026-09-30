---
id: BL-750
title: Authenticate SSH packets with hmac-sha1-etm@openssh.com and hmac-md5-etm@openssh.com
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-680]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-750 — Authenticate SSH packets with hmac-sha1-etm@openssh.com and hmac-md5-etm@openssh.com

## Goal

The SSH packet layer speaks the two encrypt-then-MAC forms no other task names: `hmac-sha1-etm@openssh.com` (in both reference builds' default MAC lists) and `hmac-md5-etm@openssh.com` (libssh 0.12.2, full set only), each in ADR-0122's position, using the BCL's `HMACSHA1` and `HMACMD5`.

## Context

- ADR-0122 records the MAC lists measured 2026-09-28. BL-565 builds the SHA-2 `-etm` forms and BL-680 the non-etm SHA-1 and MD5 MACs; this task adds the remaining two on the same seam.
- Specification: OpenSSH `PROTOCOL` section 1.7 (encrypt-then-MAC: the length is sent in the clear, the MAC covers the sequence number, length and ciphertext).

## Acceptance criteria

- [x] A session with each of the two MACs negotiated exchanges packets with ADR-0122's in-memory SSH peer, pinned by a test per MAC.
- [x] A packet whose MAC fails ends the session as BL-565 ends one, pinned by a test.
- [x] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

- BL-680 (c47504c6) already registered both MACs in `SshPacketProtections` (BCL `HMACSHA1`/`HMACMD5` behind `BclSshHmac`, `IsEncryptThenMac: true`), offered them in ADR-0122 order, and pinned `hmac-sha1-etm` in the packet round trip, the in-memory SFTP session and the reader's altered-packet test. No production change was needed; this task adds the missing pins.
- Added: `SshProtocolHandlerTests.ExecuteAsync_ServerOffersOnlyHmacMd5Etm_TransfersOverItWithTheFullSet` (the Full preset, since only libssh 0.12.2 offers hmac-md5-etm), `hmac-sha1-etm` and `hmac-md5-etm` rows in `ReExchangeKeysAsync_ServerPacketFailsItsCheck_EndsTheSessionWithLibssh2sCode` (-4, as BL-565's encrypt-then-MAC row), and an `hmac-md5-etm` row in `ReadAsync_ProtectedPacketAltered_ThrowsWithLibssh2sCode`.
- Delivered directly rather than through the full `/feature` stages: tests only, no new production code, so coverage of new code is trivially 100%.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. hmac-sha1-etm and hmac-md5-etm are pinned end to end: SFTP session over each and a failed MAC ending the session with -4
