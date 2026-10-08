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
completed: 2026-10-07
---
# BL-1667 — Take an OACK option whose name starts with blksize or tsize as that option, as curl's checkprefix does

## Goal

An option acknowledgement option whose name starts with `blksize` or `tsize` (any case) is read as that option, as curl 8.21.0's `tftp_parse_option_ack` does with `checkprefix`, or the reason it is not is pinned from a measurement.

## Context

- Found by BL-1520's adversarial tests. Input: OACK `blksizex\0256\0`, DATA 1 of 256 bytes, DATA 2 of 1 byte. Curl's `checkprefix(TFTP_OPTION_BLKSIZE, option)` matches `blksizex`, takes 256 as the block size and writes 257 bytes. `TftpOptionAcknowledgement.ReadOption` compares the whole name, ignores the option, keeps 512 and ends after block 1 with 256 bytes.
- Measure real curl against a loopback UDP server before changing the comparison, and check the same prefix rule for `tsize`.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Tftp.UnitTests` pin curl's measured answer for `blksizex` and `tsizex` option names.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.21.0 (Schannel, mingw64) with Record-CurlExchange.ps1 -Tftp -TftpNoOack, TftpData 256 `a`, and `RRQ=PACKET` injecting an OACK before DATA 1. `blksizex=256`: curl sent ACK 0, ACK 1, wrote 256 bytes, exit 0 (block size stayed 512). Control `blksize=256`: curl waited for block 2 and timed out (exit 28). `tsizex=0`: exit 0, 256 bytes. Control `tsize=0`: exit 71 "invalid tsize -::- value in OACK packet". So curl 8.21.0 compares the whole option name, not a prefix; the task's premise (checkprefix) does not hold for this build.
- No production change: `TftpOptionAcknowledgement.ReadOption` already compares whole names. Pinned by `TftpProtocolHandlerAdversarialTests.ExecuteAsync_OptionAcknowledgementNameOnlyStartsWithAKnownOption_IgnoresTheOption` (rows blksizex, tsizex). Coverage unchanged since no library code changed, so Measure-CodeQuality was not run.

## Log

- 2026-10-07: Created by BL-1520.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. OACK options named blksizex or tsizex are ignored, as curl 8.21.0 measured; pinned by tests
