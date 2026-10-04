---
id: BL-1444
title: Follow curl when a TFTP reply's opcode is curl's INIT or TIMEOUT event or switches the transfer's direction
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-035
created: 2026-10-04
completed:
---
# BL-1444 — Follow curl when a TFTP reply's opcode is curl's INIT or TIMEOUT event or switches the transfer's direction

## Goal

The four opcode cases BL-1435 measured but left ignored behave as curl 8.21.0 does: opcode 0 as the first reply re-sends the request, opcode 7 at any point acts as a timeout, an ACK as a download's first reply turns it into a transmit, and DATA as an upload's first reply turns it into a receive.

## Context

- curl reads a packet's opcode as its state machine event (`lib/tftp.c`, `tftp_receive_packet`): 0 is `TFTP_EVENT_INIT`, 7 is `TFTP_EVENT_TIMEOUT`. `tftp_send_first` connects for transmit on ACK and for receive on DATA whatever the transfer's direction.
- `Curl.Protocol.Tftp.UnitLibrary/TftpUnexpectedOpcode.cs` `IsIgnored` lists exactly these cases; `TftpDownload.AnswerUnexpectedOpcode` and `TftpUpload.AnswerUnexpectedOpcode` call it.
- Measured by BL-1435 with `Record-CurlExchange.ps1 -Tftp -TftpNoOack -TftpData <600 x> -TftpReply '<step>=PACKET <hex>'` (curl 8.21.0, mingw64, `-v`):
  - `RRQ=PACKET 00000000` (opcode 0 first): curl re-sends the RRQ, then completes on the DATA that follows; exit 0, no extra `-v` line.
  - `RRQ=PACKET 00070000` (opcode 7 first): curl re-sends the RRQ; exit 0.
  - `ACK1=PACKET 00070000` (opcode 7 after DATA 1): `* Internal error: Unexpected packet`, then `* Timeout waiting for block 2 ACK. Retries = 1`, ACK 1 re-sent; exit 0.
  - `DATA1=PACKET 00070000` on an upload: `* Internal error: Unexpected packet`, `* Timeout waiting for block 2 ACK. Retries = 1`, DATA 1 re-sent; exit 0.
  - `RRQ=PACKET 00040000` (ACK 0 as a download's first reply): `* set timeouts for state 2; Total 0, retry 5 maxtry 3`, curl sends an empty DATA 1, then the server's DATA 1 gives `* tftp_tx: internal error, event: 3`; with no ACK 1 it ends exit 28 `tftp_tx: internal error, event: 3` after its retries.
  - DATA as an upload's first reply: not measured; measure it first (`WRQ=PACKET 0003000141`).

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` pin each measured case above: the packets sent, the `-v` lines in order, the exit code and message.
- [ ] The upload's first-reply DATA case is measured, its `stderr.txt` and exit code put in Notes, and pinned.
- [ ] `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
