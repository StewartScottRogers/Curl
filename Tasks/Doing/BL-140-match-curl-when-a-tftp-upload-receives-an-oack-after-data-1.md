---
id: BL-140
title: Match curl when a TFTP upload receives an OACK after DATA 1
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-127]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-140 — Match curl when a TFTP upload receives an OACK after DATA 1

## Goal

A TFTP upload that receives an OACK after DATA 1 has gone does what curl 8.21.0 does, instead of ignoring it.

## Context

- Found by BL-127. Measured with curl 8.21.0 against a loopback Python UDP server,
  1000-byte upload: server sent ACK 0, then answered DATA 1 (512 bytes) with
  `OACK blksize 8`. curl then sent DATA **block 1 with 8 bytes**, and after ACK 1 sent
  DATA 2 with 8 bytes; the server fell silent and curl ended with exit 28
  `Timeout was reached` at about 23 s. Which 8 bytes block 1 carried, and whether curl
  restarts from the start of the file, was not recorded.
- curl 8.21.0 `lib/tftp.c`: `tftp_receive_packet` parses every OACK
  (`tftp_parse_option_ack`), whatever the state; see how `tftp_tx` handles
  `TFTP_EVENT_OACK`.
- `TftpUpload.AnswerAsync` ignores an OACK once `firstBlockSent` is set; the test
  `TftpUploadTests.ExecuteAsync_LateOptionAckAndUnexpectedOpcode_AreIgnored` pins that.
- If curl's result is a bug not worth matching, file a Stewart decision task rather than
  diverging.

## Acceptance criteria

- [ ] `Notes` records the measured bytes of every DATA packet curl sends after a late
      OACK, and the exit code and message.
- [ ] A test pins the measured behaviour, replacing the late-OACK half of
      `ExecuteAsync_LateOptionAckAndUnexpectedOpcode_AreIgnored`.
- [ ] `dotnet build` is clean, the fast tests are green, and
      `Curl.Protocol.Tftp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
