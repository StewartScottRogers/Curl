---
id: BL-129
title: Match curl when a TFTP download receives a datagram under four bytes
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-127]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-129 — Match curl when a TFTP download receives a datagram under four bytes

## Goal

A TFTP download that receives a datagram under four bytes does what curl 8.21.0 does, instead of ignoring it.

## Context

- Found by BL-127. curl 8.21.0's `tftp_receive_packet` turns a datagram under four
  bytes into `TFTP_EVENT_TIMEOUT` after `failf("Received too short packet")`. Measured
  for an upload (BL-127 Notes): the last packet is re-sent at once and a retry counted,
  and the transfer's eventual failure message is `Received too short packet`.
  `TftpUpload` now does this.
- `TftpDownload.AnswerAsync` still ignores such a datagram. The download case has not
  been measured: measure it first (short datagram in reply to the RRQ, and after DATA 1)
  with curl 8.21.0 against a loopback Python UDP server.

## Acceptance criteria

- [ ] `Notes` records what curl 8.21.0 sends and when, and the exit code and message,
      for a short datagram in reply to the RRQ and after DATA 1.
- [ ] A test on the fake clock pins each measured case in `TftpDownloadRetransmissionTests`.
- [ ] `dotnet build` is clean, the fast tests are green, and
      `Curl.Protocol.Tftp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-26: Created.
