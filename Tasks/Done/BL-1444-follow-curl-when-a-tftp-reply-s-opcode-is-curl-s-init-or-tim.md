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
completed: 2026-10-04
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

- [x] Tests in `Curl.Protocol.Tftp.UnitTests` pin each measured case above: the packets sent, the `-v` lines in order, the exit code and message.
- [x] The upload's first-reply DATA case is measured, its `stderr.txt` and exit code put in Notes, and pinned.
- [x] `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- Re-measured every case 2026-10-04 (curl 8.21.0 mingw64, `-v`, `-TftpNoOack`, 600-byte file or upload). Correction to the Context: opcode 0 or 7 as the first reply *does* print `* Internal error: Unexpected packet`, for downloads and uploads alike (the earlier reading lost lines that shared a progress-meter line).
- Upload, first reply DATA (`WRQ=PACKET 0003000141`): exit 0. `stderr.txt` `-v` lines: `Trying 127.0.0.1:<port>...`, `Established connection ...`, `set timeouts for state 0; Total 300000, retry 6 maxtry 50`, `{ [1 bytes data]`, `Connected for receive`, `set timeouts for state 1; Total 0, retry 5 maxtry 3`, `shutting down connection #0`. curl sends ACK 1 and writes `A` to stdout.
- Download, first reply ACK 0: `Connected for transmit`, `set timeouts for state 2; ...`, `tftp_tx: internal error, event: 3`, `Timeout waiting for block 2 ACK. Retries = 1` to `4`; empty DATA 1 sent four times; exit 28 `tftp_tx: internal error, event: 3`.
- Design (ADR-0414): a direction switch hands over to the other class's `TakeOverAsync` with a `TftpHandOver`; every `failf` is a noted failure whose message the transfer keeps, generalising the too-short note, so an upload's `tftp_tx: internal error, event: N` is now noted too.
- `TftpProtocolHandlerTests.ExecuteAsync_FirstReplyAckTimeoutOpcodeAndWrongBlock_AreIgnored` pinned the old ignoring; it now pins only the wrong-block case, as `ExecuteAsync_FirstReplyOfTheWrongBlock_IsIgnored`.
- Measure-CodeQuality (Tftp library): 100% line and branch, 0 failing members after splitting both `AnswerAsync` methods. Full build clean; all 33 fast test projects pass.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. TFTP opcodes 0 and 7 re-send or time out, and a first reply that switches direction hands the transfer over, as curl 8.21.0 does
