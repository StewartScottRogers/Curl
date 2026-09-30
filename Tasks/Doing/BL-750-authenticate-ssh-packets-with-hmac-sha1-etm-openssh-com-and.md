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
completed:
---
# BL-750 — Authenticate SSH packets with hmac-sha1-etm@openssh.com and hmac-md5-etm@openssh.com

## Goal

The SSH packet layer speaks the two encrypt-then-MAC forms no other task names: `hmac-sha1-etm@openssh.com` (in both reference builds' default MAC lists) and `hmac-md5-etm@openssh.com` (libssh 0.12.2, full set only), each in ADR-0122's position, using the BCL's `HMACSHA1` and `HMACMD5`.

## Context

- ADR-0122 records the MAC lists measured 2026-09-28. BL-565 builds the SHA-2 `-etm` forms and BL-680 the non-etm SHA-1 and MD5 MACs; this task adds the remaining two on the same seam.
- Specification: OpenSSH `PROTOCOL` section 1.7 (encrypt-then-MAC: the length is sent in the clear, the MAC covers the sequence number, length and ciphertext).

## Acceptance criteria

- [ ] A session with each of the two MACs negotiated exchanges packets with ADR-0122's in-memory SSH peer, pinned by a test per MAC.
- [ ] A packet whose MAC fails ends the session as BL-565 ends one, pinned by a test.
- [ ] `dotnet build` clean, fast tests green, 100% line and branch coverage of the new code.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
