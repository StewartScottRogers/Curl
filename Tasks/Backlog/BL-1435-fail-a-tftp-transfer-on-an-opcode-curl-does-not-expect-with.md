---
id: BL-1435
title: Fail a TFTP transfer on an opcode curl does not expect with exit 71 and curl's internal-error messages
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-035
created: 2026-10-04
completed:
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

- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` pin, for a download: an ACK as the first reply and after DATA block 1 fails with exit 71 and the measured message; an RRQ, a WRQ and opcode 9 at either point fail with exit 71 and `Internal error: Unexpected packet`; the `-v` lines are the measured ones in the measured order.
- [ ] Tests pin, for an upload: a DATA packet or opcode 9 while it waits for an ACK writes the measured `-v` line(s) and the upload still completes with exit 0 when the expected ACK follows.
- [ ] Every existing TFTP test passes unchanged; `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
