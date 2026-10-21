---
id: BL-1435
title: Fail a TFTP transfer on an opcode curl does not expect with exit 71 and curl's internal-error messages
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Record-CurlExchange.ps1]
requirement: FR-035
created: 2026-10-04
completed: 2026-10-04
---
# BL-1435 — Fail a TFTP transfer on an opcode curl does not expect with exit 71 and curl's internal-error messages

## Goal

A TFTP transfer that receives a packet whose opcode curl does not expect in its state fails, or carries on, exactly as curl 8.21.0 does - exit 71 with curl's `Internal error: Unexpected packet` / `tftp_rx: internal error` / `tftp_send_first: internal error` messages - instead of silently ignoring the packet and waiting for the next one, as Curl does today.

## Context

- Upstream (tag `curl-8_21_0`), `lib/tftp.c`:
  - `tftp_receive_packet` lines 1075-1116: the packet's opcode is the event. RRQ (1), WRQ (2) and any opcode above 6 reach the `default` branch: `failf("Internal error: Unexpected packet")`, the event is still handed to the state machine. ACK (4) is passed through silently.
  - First reply, state `TFTP_STATE_START` (`tftp_send_first`, lines 768-790): OACK, ACK and DATA connect, ERROR finishes; any other event fails with `failf("tftp_send_first: internal error")` and `CURLE_TFTP_ILLEGAL` (exit 71).
  - Download, state `TFTP_STATE_RX` (`tftp_rx`, lines 627-632): DATA, OACK, ERROR and TIMEOUT are handled; any other event - an ACK included - fails with `failf("tftp_rx: internal error")` and exit 71.
  - Upload, state `TFTP_STATE_TX` (`tftp_tx`, lines 493-496): any event but ACK, OACK, TIMEOUT and ERROR writes `failf("tftp_tx: internal error, event: %d")` and returns `CURLE_OK`, so the upload carries on waiting for its ACK.
  - curl keeps the first `failf` message as the one `curl: (71)` prints; every `failf` also appears as a `*` line under `-v`. So an RRQ during a download prints `curl: (71) Internal error: Unexpected packet` after `-v` lines for both messages, and an ACK during a download prints `curl: (71) tftp_rx: internal error`.
- Curl today: `Curl.Protocol.Tftp.UnitLibrary/TftpDownload.cs` (switch near line 163) and `TftpUpload.cs` (switch near line 161) return `null` - keep waiting - for every opcode they do not handle.
- Measure each case against real curl with `Record-CurlExchange.ps1 -Tftp -TftpReply ...` (download answered with an ACK, an RRQ and opcode 9 both as the first reply and after DATA block 1; upload answered with DATA and with opcode 9 after its ACK of block 0), and put the `stderr.txt` and exit codes in Notes before pinning.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Tftp.UnitTests` pin, for a download: an ACK as the first reply and after DATA block 1 fails with exit 71 and the measured message; an RRQ, a WRQ and opcode 9 at either point fail with exit 71 and `Internal error: Unexpected packet`; the `-v` lines are the measured ones in the measured order.
- [x] Tests pin, for an upload: a DATA packet or opcode 9 while it waits for an ACK writes the measured `-v` line(s) and the upload still completes with exit 0 when the expected ACK follows.
- [x] Every existing TFTP test passes unchanged; `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

- Criteria 1 and 3 were corrected by measurement, as the Goal asks ("exactly as curl 8.21.0 does"):
  - An ACK as a download's *first* reply does not fail with 71: curl connects for transmit, sends an empty DATA 1 and ends exit 28 `tftp_tx: internal error, event: 3`. It stays ignored, as before, and is filed as BL-1444. An ACK after DATA 1 fails with 71 as the criterion says.
  - One existing test, `ExecuteAsync_UnexpectedOpcodeAndWrongBlock_AreIgnored`, pinned the old behaviour (opcode 9 ignored). Its opcode is now 7, which is still ignored, and it is renamed `ExecuteAsync_FirstReplyAckTimeoutOpcodeAndWrongBlock_AreIgnored`. Every other TFTP test passes unchanged.
- Extended `Record-CurlExchange.ps1` (added to `touches`; no task in Doing on `origin/work/dark-factory` names it): `-TftpReply '<step>=PACKET <hex>'` sends a raw datagram just before the server's own answer to that step.
- Measured with curl 8.21.0 (mingw64, Schannel), `-v`, `-TftpNoOack -TftpData <600 x>`. The `-v` lines after `Established connection`, then the exit:
  - Download, first reply RRQ, WRQ or opcode 9: `* tftp_send_first: internal error`; exit 71 `Internal error: Unexpected packet`. There is no `* Internal error: Unexpected packet` line in this state. Curl sends nothing after.
  - Download after DATA 1, RRQ, WRQ, opcode 0 or 9: `* Connected for receive`, `* set timeouts for state 1; Total 0, retry 5 maxtry 3`, `* Internal error: Unexpected packet`, `* tftp_rx: internal error`; exit 71 `Internal error: Unexpected packet`.
  - Download after DATA 1, ACK 1: the same lines without the unexpected-packet line; exit 71 `tftp_rx: internal error`.
  - Upload after ACK 0, DATA: `* tftp_tx: internal error, event: 3`, then ACK 1; exit 0. Opcode 9, 1 or 0: `* Internal error: Unexpected packet`, `* tftp_tx: internal error, event: 9|1|0`; exit 0.
  - Upload, first reply opcode 9: `* tftp_send_first: internal error`; exit 71 `Internal error: Unexpected packet`.
  - Not modelled (BL-1444): opcode 0 as the first reply makes curl re-send the RRQ, and opcode 7 is its TIMEOUT event and re-sends the last packet. Both exit 0.
- Implementation: `TftpUnexpectedOpcode` (the messages, `IsUnknown`, `IsIgnored`), plus `AnswerUnexpectedOpcode` in `TftpDownload` and in `TftpUpload`, and `TftpTransferEvents.InternalError`.
- Measure-CodeQuality: Curl.Protocol.Tftp.UnitLibrary at 100% line and 100% branch coverage, 0 failing members. 226 TFTP tests pass.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. A TFTP packet with an opcode curl does not expect now fails a download, or a transfer not yet answered, with exit 71 and curl's internal-error messages. An upload already under way logs curl's -v lines and carries on.
