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
completed: 2026-09-26
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

- [x] `Notes` records the measured bytes of every DATA packet curl sends after a late
      OACK, and the exit code and message.
- [x] A test pins the measured behaviour, replacing the late-OACK half of
      `ExecuteAsync_LateOptionAckAndUnexpectedOpcode_AreIgnored`.
- [x] `dotnet build` is clean, the fast tests are green, and
      `Curl.Protocol.Tftp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Measured 2026-09-26, curl 8.21.0 (mingw64, Schannel) against a loopback Python UDP
  server answering from a second port. Upload of 1000 bytes, byte i = (7i + i/256) mod 251.
  Server: ACK 0, then answered DATA 1 (512 bytes, file bytes 0-511) with `OACK blksize 8`.
  - curl's next DATA was **block 1, 8 bytes, file bytes 512-519** (`484f565d646b7279`):
    it does not restart the file; it continues from the stream position at the new size.
  - After ACK 1: DATA 2, bytes 520-527 (`80878e959ca3aab1`).
  - Server silent from there: DATA 2 re-sent at 6.07, 12.19, 18.25 s; curl ended with
    exit 28 `curl: (28) Timeout was reached` at about 24 s.
  - Server ACKing everything instead: DATA 1..61 of 8 bytes (bytes 512-999), then an
    empty DATA 62, ACK 62, exit 0.
  - 3-byte upload (`abc`), OACK blksize 8 after DATA 1: curl sent an **empty DATA 1**,
    and ACK 1 ended it with exit 0.
- Matches curl 8.21.0 `lib/tftp.c` `tftp_tx`: `TFTP_EVENT_OACK` sets `state->block = 1`
  and reads the next `blksize` bytes, whatever the state, so this is curl's designed
  path, not a bug worth a Stewart decision.
- Change: `TftpUpload.AnswerAsync` takes every OACK: new `blksize`, block count back to 0,
  `lastBlockSent` cleared, then treated as ACK 0. The `firstBlockSent` field is gone.
- Tests: `ExecuteAsync_LateOptionAckAndUnexpectedOpcode_AreIgnored` split into
  `ExecuteAsync_UnexpectedOpcode_IsIgnored`,
  `ExecuteAsync_OptionAckAfterData1_RestartsAtBlock1WithTheNextBytesInTheAcknowledgedBlockSize`
  and `ExecuteAsync_OptionAckAfterTheLastBlock_SendsAnEmptyData1`.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary`: 100% line, 100%
  branch. It reports one pre-existing failing member, `TftpDownload.AnswerAsync`
  (Cobertura complexity 11; the build's CA1502 passes). Untouched here; `TftpDownload.AnswerAsync`
  is the method BL-141 changes, so it is left for that task.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. A TFTP upload that gets an OACK after DATA 1 takes its blksize and restarts at block 1 with the next bytes, as curl 8.21.0 does
