---
id: BL-127
title: Implement TFTP upload retransmission and the timeout that ends a silent upload
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-075]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-127 — Implement TFTP upload retransmission and the timeout that ends a silent upload

## Goal

A TFTP upload (`-T`) whose server goes silent re-sends its write request or last DATA
block on curl 8.21.0's schedule and ends as curl does, instead of waiting on
`ReceiveAsync` until the token is cancelled, and its write request's `timeout` option
follows the same schedule instead of always `6`.

## Context

- BL-075 did this for the download only (`TftpDownload`, `TftpRetrySchedule`); see
  `Curl.Protocol.Tftp.UnitLibrary/CLAUDE.md` for the rules it follows. `TftpUpload.cs`
  still waits with no timeout and `TftpPackets.BuildWriteRequest` always sends
  `timeout 6`.
- Reuse `TftpRetrySchedule` and the test fakes `ManualTimeProvider` and
  `FallsSilentDatagramChannel` under `Curl.Protocol.Tftp.UnitTests/Fakes/`.
- Nothing about a silent upload server has been measured yet. Measure first with curl
  8.21.0 against a loopback Python UDP server (`curl -T file tftp://127.0.0.1:<port>/x`
  with no options, `--connect-timeout 10`, and `-m 5`): the WRQ `timeout` value, the
  resend times, which packet is re-sent after the first ACK, and the exit code and
  message. Record the numbers in `Notes` before writing code; if curl's result cannot be
  reproduced, file a Stewart decision task instead of diverging.

## Acceptance criteria

- [ ] `Notes` records the measured WRQ `timeout`, resend times, exit code and message
      for a silent server with no options, `--connect-timeout 10` and `-m 5`, and for a
      server that falls silent after ACK 0.
- [ ] A test on the fake clock asserts each measured case: the packets re-sent, their
      times, and the `CurlExitCode` and message the upload ends with.
- [ ] A datagram from an endpoint other than the pinned server ends the upload as
      measured (the download ends with exit 56 `Data received from another address`).
- [ ] No test uses `Thread.Sleep` or a real-clock delay; every wait in `TftpUpload`
      goes through `ITransferContext.TimeProvider`.
- [ ] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` is
      green, and `Curl.Protocol.Tftp.UnitLibrary` has 100% line and branch coverage.

## Notes

- Filed 2026-09-26 by BL-075 as follow-up work.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
