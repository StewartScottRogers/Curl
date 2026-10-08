---
id: BL-1666
title: Restart a tftp download at block 1 when an option acknowledgement arrives after DATA
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1666 — Restart a tftp download at block 1 when an option acknowledgement arrives after DATA

## Goal

A `tftp://` download that receives an option acknowledgement after it has started taking DATA answers it with ACK 0 and expects block 1 again, as curl 8.21.0 does, so the next DATA 1 is written as new data.

## Context

- Found by BL-1520's adversarial tests. Input: DATA 1 (512 bytes `a`), then OACK `blksize\0512\0`, then DATA 1 (`bb`). Curl's `tftp_rx` handles `TFTP_EVENT_OACK` by setting `state->block = 0` and sending ACK 0, so the second DATA 1 is the expected block and is written: 514 bytes. `TftpDownload.AcceptOptionAcknowledgementAsync` sends ACK 0 but keeps `expectedBlock` at 2, so the second DATA 1 is taken as a repeat and the download ends with 512 bytes.
- The upload side already restarts (`TftpUploadTests.ExecuteAsync_OptionAckAfterData1_RestartsAtBlock1WithTheNextBytesInTheAcknowledgedBlockSize`).
- Measure real curl with a loopback UDP server (extend `Record-CurlExchange.ps1` if it has no TFTP mode) before pinning the bytes; check curl's `lib/tftp.c` for the version matched.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Tftp.UnitTests` sends DATA 1, OACK, DATA 1 (short) and asserts the output is both blocks' bytes, ACKs 1, 0, 1, and exit 0, matching measured curl.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created by BL-1520.
- 2026-10-07: Backlog -> Doing.
