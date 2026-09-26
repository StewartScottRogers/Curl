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
completed: 2026-09-26
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

- [x] `Notes` records the measured WRQ `timeout`, resend times, exit code and message
      for a silent server with no options, `--connect-timeout 10` and `-m 5`, and for a
      server that falls silent after ACK 0.
- [x] A test on the fake clock asserts each measured case: the packets re-sent, their
      times, and the `CurlExitCode` and message the upload ends with.
- [x] A datagram from an endpoint other than the pinned server ends the upload as
      measured (the download ends with exit 56 `Data received from another address`).
- [x] No test uses `Thread.Sleep` or a real-clock delay; every wait in `TftpUpload`
      goes through `ITransferContext.TimeProvider`.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` is
      green, and `Curl.Protocol.Tftp.UnitLibrary` has 100% line and branch coverage.

## Notes

- Filed 2026-09-26 by BL-075 as follow-up work.

### Measured with curl 8.21.0 (mingw64, Schannel), 2026-09-26, lane 1

Loopback Python UDP servers answering from a second (transfer) port, a 1000-byte upload,
`curl -T up.bin tftp://127.0.0.1:<port>/x`.

- **Silent server, no options:** WRQ `tsize 1000`, `blksize 512`, **`timeout 6`**; 50
  byte-identical WRQs from one source port at t = 0, 7.08, 14.11, ... 345.52 s (about
  every 7.05 s), then exit **7** `curl: (7) Could not connect to server`.
- **Silent server, `--connect-timeout 10`:** WRQ `timeout 3`; WRQs at 0, 4.04, 8.09 s,
  then exit **7** `Could not connect to server`.
- **Silent server, `-m 5`:** WRQ `timeout 1`; WRQs at 0, 2.02, 4.05 s, then exit **28**
  `Operation timed out after 5010 milliseconds with 0 bytes received`.
- **ACK 0, then silent (no options):** DATA 1 (512 bytes) to the transfer port at 0,
  6.07, 12.14, 18.20 s, then exit **28** `Timeout was reached`. The same with
  `--connect-timeout 10` (WRQ `timeout 3`; DATA 1 at 0, 6.07, 12.03, 18.08 s, exit 28).
  With `-m 5` (WRQ `timeout 1`): DATA 1 at 0, 2.02, 4.05 s, then exit 28
  `Operation timed out after 5010 milliseconds with 0 bytes received` - 0 bytes
  *received*, although 512 were sent.
- **Stranger:** ACK 0 from the transfer port, then ACK 1 from a third port: curl sends
  the stranger nothing and ends at once with exit **56**
  `Data received from another address`.
- **ACK of the wrong block after DATA 1** (ACK 0 again): DATA 1 re-sent at once; the
  transfer then completes (exit 0). Four in a row: DATA 1 re-sent three times, then exit
  **55** `tftp_tx: giving up waiting for block 1 ack`, all at t = 0.
- **ACK 5 in reply to the WRQ:** curl sends a 4-byte packet, the WRQ's first four bytes
  (`00 02 'x' 00`), to the listening port, then continues normally on ACK 0 (exit 0).
- **Datagram under four bytes** (`00 04`): in reply to the WRQ, the WRQ is re-sent at
  once, then on the usual 7 s schedule; after DATA 1, DATA 1 is re-sent at once; four in
  a row after DATA 1 end with exit **28** at t = 0. The failure message is then
  `Received too short packet` whatever ends the transfer: measured for exit 7 (WRQ),
  exit 28 (retries), exit 55 (one short datagram then four wrong ACKs) and exit 28 from
  `-m 5` (one short datagram then silence).
- **DATA packet received during an upload:** ignored; DATA 1 re-sent at 6.05 s.
- **OACK `blksize 8` after DATA 1:** curl sends DATA block 1 again with 8 bytes, then
  DATA 2 with 8 bytes after ACK 1 (not matched; filed as BL-128).

These are curl 8.21.0's `tftp_set_timeouts` rules as recorded under BL-075, applied to
the WRQ; after the first ACK/OACK the schedule is 3 retries 6 s apart (15 s default).

### Delivered 2026-09-26 (lane 1)

- `TftpTimeLimits` (new) holds what `TftpDownload` did privately - the request and
  answered schedules, the wait until the next re-send or `MaxTime`, and the `MaxTime`
  failure - and both `TftpDownload` and `TftpUpload` use it.
  `TftpPackets.BuildWriteRequest` takes the `timeout` from the schedule; the handler
  passes the start timestamp to the upload.
- `TftpUpload` re-sends the WRQ or the last DATA block on the schedule, ends with 7 / 28
  / 28 (`MaxTime`, `0 bytes received`) / 55 / 56 as measured, and re-sends at once on a
  wrong ACK or a too-short datagram without moving the scheduled re-send.
- Tests: `TftpUploadRetransmissionTests` (14, on `ManualTimeProvider` and
  `FallsSilentDatagramChannel`); `TftpRetransmissionTests` renamed
  `TftpDownloadRetransmissionTests`, since it covers the download only (review
  finding). `ExecuteAsync_ShortDatagramWrongAckLateOptionAckAndUnexpectedOpcode_AreIgnored`
  became `ExecuteAsync_LateOptionAckAndUnexpectedOpcode_AreIgnored`: the short datagram
  and wrong ACK are no longer ignored. Tftp tests 61 -> 75; library at 100% line and
  branch.
- Choices taken (sensible defaults):
  - After ACK 5 to the WRQ, curl's 4-byte re-send is matched byte for byte, since it was
    measured and it is what a server sees; it looks like a curl quirk (it re-sends
    `4 + sbytes` bytes of its send buffer while `sbytes` is still 0).
  - A too-short datagram and a wrong ACK do not move the next scheduled re-send, since
    curl's `rx_time` is set only by the timeout and a good ACK.
  - The `Received too short packet` message replaces every later failure's message,
    including exit 56 and an ERROR packet's (unmeasured, but the same first-`failf`-wins
    rule that was measured for 7, 28 and 55).
  - curl's acceptance of ACK 65535 when block 0 is expected (block-number wrap) is not
    matched; it needs a 32 MiB upload to reach and was not measured.
- Follow-ups filed: BL-128 (late OACK during an upload), BL-129 (too-short datagram
  during a download, unmeasured).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TFTP upload re-sends its WRQ or last DATA block on curl 8.21.0's schedule and ends with exit 7/28/55/56 as measured
