---
id: BL-1326
title: Accept an ACK of block 65535 when a TFTP upload expects block 0, as curl does for tftpd-hpa
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-034
created: 2026-10-03
completed: 2026-10-03
---
# BL-1326 — Accept an ACK of block 65535 when a TFTP upload expects block 0, as curl does for tftpd-hpa

## Goal

`TftpUpload.AcceptAcknowledgementAsync` treats an ACK of block 65535 as the expected acknowledgement whenever the block it is waiting for is 0 (the write request's own ACK, and the ACK after the block number wraps from 65535 to 0), as curl 8.21.0's `tftp_tx` does, instead of writing `Received ACK for block 65535, expecting 0` and re-sending.

## Context

- Today `Curl.Protocol.Tftp.UnitLibrary/TftpUpload.cs` `AcceptAcknowledgementAsync` (line 218) compares `block != lastSentBlock` and, on a mismatch, reports `TftpTransferEvents.UnexpectedAcknowledgement` (`Received ACK for block N, expecting M`) and re-sends, failing with exit 55 `tftp_tx: giving up waiting for block N ack` once the retries run out. BL-1304's Notes record this gap.
- curl 8.21.0, `lib/tftp.c` `tftp_tx` lines 371-385 (tag `curl-8_21_0`): `if(rblock != state->block && !(state->block == 0 && rblock == 65535))` - "There is a bug in tftpd-hpa that causes it to send us an ACK for 65535 when the block number wraps to 0. To handle it, when we are expecting 0, also accept 65535." `state->block` is 0 both while the write request waits for its ACK and right after a wrap; in both cases an ACK of 65535 is taken as the expected one, so curl sends the next DATA block.
- Downloads are not affected: `tftp_rx` has no such exception.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Tftp.UnitTests` runs an upload through the fake datagram channel whose server answers the write request with `ACK 65535` and asserts DATA block 1 is sent next, no `Received ACK for block 65535, expecting 0` info line is reported, and the upload completes with exit 0.
- [x] A test runs an upload long enough to wrap (more than 65535 blocks of the smallest block size the tests allow, or a seam that starts the block counter near the wrap if one exists; say which in the test comment), answers the ACK expected as block 0 with `ACK 65535`, and asserts the next block is sent without a resend.
- [x] A test pins that an ACK of 65535 while expecting any block other than 0 is still unexpected: the `Received ACK for block 65535, expecting N` line and a resend, as today.
- [x] `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- `TftpUpload.IsExpectedAcknowledgement` accepts ACK 65535 while block 0 is awaited (`lastSentBlock == 0`), matching curl 8.21.0 `tftp_tx`; the OACK path, which passes block 0, is unaffected. Tests are in `TftpUploadBlockWrapTests`. The wrap test runs 65537 real blocks at OACK `blksize 8`, because no seam starts the counter near the wrap. Delivered directly rather than through the full `/protocol` stages: it is a one-line condition. 209 TFTP tests pass; Measure-CodeQuality reports 0 failing members.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. A TFTP upload takes ACK 65535 as the awaited ACK of block 0 (WRQ and after the wrap), as curl does for tftpd-hpa
