---
id: BL-1667
title: Take an OACK option whose name starts with blksize or tsize as that option, as curl's checkprefix does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1667 — Take an OACK option whose name starts with blksize or tsize as that option, as curl's checkprefix does

## Goal

An option acknowledgement option whose name starts with `blksize` or `tsize` (any case) is read as that option, as curl 8.21.0's `tftp_parse_option_ack` does with `checkprefix`, or the reason it is not is pinned from a measurement.

## Context

- Found by BL-1520's adversarial tests. Input: OACK `blksizex\0256\0`, DATA 1 of 256 bytes, DATA 2 of 1 byte. Curl's `checkprefix(TFTP_OPTION_BLKSIZE, option)` matches `blksizex`, takes 256 as the block size and writes 257 bytes. `TftpOptionAcknowledgement.ReadOption` compares the whole name, ignores the option, keeps 512 and ends after block 1 with 256 bytes.
- Measure real curl against a loopback UDP server before changing the comparison, and check the same prefix rule for `tsize`.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` pin curl's measured answer for `blksizex` and `tsizex` option names.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created by BL-1520.
- 2026-10-07: Backlog -> Doing.
