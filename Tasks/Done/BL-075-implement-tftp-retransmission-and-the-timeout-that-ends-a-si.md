---
id: BL-075
title: Implement TFTP retransmission and the timeout that ends a silent transfer
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-045, BL-123]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-075 — Implement TFTP retransmission and the timeout that ends a silent transfer

## Goal

A TFTP download whose server goes silent re-sends its last packet on curl 8.21.0's
schedule and then ends exactly as curl does - exit 7 `Could not connect to server` when
nothing ever answered the RRQ, exit 28 `Timeout was reached` when the server fell silent
mid-transfer - instead of waiting on `ReceiveAsync` forever; a duplicate DATA block is
re-acknowledged and a datagram from a stranger ends the transfer with exit 56.

## Context

- Builds on BL-045. Today `Curl.Protocol.Tftp.UnitLibrary/TftpDownload.cs` waits on
  `IDatagramChannel.ReceiveAsync` with no timeout, so a silent server hangs the transfer,
  and it ignores a duplicate DATA block instead of re-acknowledging it.
- Depends on BL-123, which adds `TimeSpan? ConnectTimeout` and `TimeSpan? MaxTime` to
  `ITransferContext` and `TransferContext`. Tests set them directly on a
  `TransferContext`; the command-line wiring (BL-122, BL-124) is not needed for this task.
- Seam: `IDatagramChannel` / `IDatagramConnector` (ADR-0005,
  `Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`).
  Every wait goes through `ITransferContext.TimeProvider`: no `Thread.Sleep`, no real
  delays in tests. Add a hand-written fake `TimeProvider` under
  `Curl.Protocol.Tftp.UnitTests/Fakes/` beside `ScriptedDatagramChannel.cs` and
  `RecordingDatagramConnector.cs` (no mocking package; MSTest only).
- Exit codes in `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`:
  `CouldntConnect` = 7, `OperationTimedOut` = 28, `RecvError` = 56. The message is
  `TransferResult.ErrorMessage` (the text after `curl: (N) `).
- Every number below is in `Notes` ("Measured with curl 8.21.0" and "The rules behind
  these numbers"), measured 2026-09-26 against loopback Python UDP servers. Derive the
  schedule from those rules (`timeout` = seconds left, else 15; `retry_max` =
  clamp(timeout / 5, 3, 50); `retry_time` = max(1, timeout / retry_max); resend interval
  `retry_time + 1` s; recomputed when the first DATA/OACK arrives) rather than
  hard-coding each case.
- The measured times are wall-clock with jitter (7.07 s, 351 s); tests on the fake clock
  assert the whole-second values the rules give (7 s, 350 s) and the measured count of
  packets.
- On an unknown endpoint curl diverges from RFC 1350 section 4 (it sends no ERROR 5).
  Match curl, as this task's Context always said; this is not a divergence from upstream.

## Acceptance criteria

- [x] Silent server, `ConnectTimeout` and `MaxTime` both `null`: a test asserts the RRQ
      (`tsize 0`, `blksize 512`, `timeout 6`) is sent 50 times, byte-identical and from
      the same channel, 7 s apart on the fake clock, and that once the 50th interval
      elapses (t = 350 s) the transfer ends with `CurlExitCode.CouldntConnect` (7) and
      `Could not connect to server`.
- [x] Silent server, `ConnectTimeout = 10 s`: a test asserts the RRQ carries `timeout 3`,
      is sent 3 times 4 s apart (t = 0, 4, 8), and the transfer ends at t = 12 s with
      `CurlExitCode.CouldntConnect` (7) and `Could not connect to server`.
- [x] Server that answers with one full DATA block 1 from a new port and then falls
      silent, `ConnectTimeout = 10 s`: a test asserts ACK 1 is sent at once and re-sent
      3 times, 6 s apart, to the transfer endpoint (not the initial server port), and the
      transfer ends one interval after the third re-send (t = 24 s by the rules in Notes;
      measured about 25 s wall-clock) with `CurlExitCode.OperationTimedOut` (28) and
      `Timeout was reached`.
- [x] Duplicate DATA block: a test asserts a repeated DATA 1 is answered with ACK 1 again
      to the transfer endpoint, its payload is not written a second time, and the transfer
      then completes with exit 0 and the correct byte count.
- [x] Datagram from an unknown endpoint (DATA 2 from a second port after DATA 1 from the
      transfer port): a test asserts nothing is sent to the stranger and the transfer ends
      at once with `CurlExitCode.RecvError` (56) and `Data received from another address`,
      reporting the bytes already written.
- [x] `MaxTime`: `Notes` records, measured with curl 8.21.0 as `curl -m 5
      tftp://127.0.0.1:<port>/file.txt` against a silent server, the RRQ `timeout` option,
      the resend times, and the exit code and message it ends with; a test with
      `MaxTime = 5 s` asserts the same. If curl's result cannot be reproduced, stop and
      file a Stewart decision task instead of diverging.
- [x] No test uses `Thread.Sleep`, `Task.Delay` on the real clock, or
      `TestCategory=Integration`; every wait in `TftpDownload` goes through
      `ITransferContext.TimeProvider`.
- [x] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` are clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `Curl.Protocol.Tftp.UnitLibrary` has 100% line and 100% branch coverage.

## Notes

### Blocked 2026-09-26 (dark factory lane 1): needs work outside `touches`

Nothing in the solution carries `--connect-timeout`: `ITransferContext` has no connect
timeout (or `--max-time`) property and no `.cs` file mentions one. curl derives TFTP's
RRQ `timeout` option, its retry interval and its retry count from the time left
(`tftp_set_timeouts`), so acceptance criterion 3 cannot be met without adding a
connect-timeout property to `Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`
and its `--connect-timeout` parse/plumbing in the CLI. Both are outside
`[Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]`, and an Abstractions change
runs apart from every protocol task. Suggested re-plan: (1) a task adding
`ConnectTimeout` (and `MaxTime`, which curl also counts) to `ITransferContext` and wiring
`--connect-timeout`/`-m`; (2) this task depending on it, with the criteria below corrected.
No code was written in this run.

### Measured with curl 8.21.0 (mingw64, Schannel) against loopback Python UDP servers

- **Silent server, no options:** 50 byte-identical RRQs (`tsize 0`, `blksize 512`,
  `timeout 6`) from one source port at t = 0, 7.07, 14.12, 21.22, ... 344.35 s (about
  every 7.05 s), then exit **7** (`CurlExitCode.CouldntConnect`) with
  `curl: (7) Could not connect to server` at about 351 s. **Not 28**: the Goal and
  criterion 2 assume 28 and must be corrected. The 300 s default connect timeout does
  not end it.
- **Silent server, `--connect-timeout 10`:** RRQ carries `timeout 3`; RRQs at 0, 4.05,
  8.08 s, then exit **7** `Could not connect to server` at 12.1 s.
- **Server silent after DATA 1, `--connect-timeout 10`** (from the task's Context): ACK 1
  re-sent to the transfer port every ~6 s, exit 28 `Timeout was reached` at ~25 s.
- **Duplicate DATA block:** curl re-ACKs it (ACK 1 sent again to the transfer endpoint)
  and does not write its payload a second time; the transfer then completes (522 bytes
  written, exit 0). Today's `TftpDownload` ignores the duplicate instead.
- **Datagram from an unknown endpoint** (DATA 2 from a second port after DATA 1 from the
  transfer port): curl sends **nothing** to the stranger (no RFC 1350 ERROR 5) and ends
  the transfer at once with exit **56** (`RecvError`) and
  `curl: (56) Data received from another address`. This diverges from RFC 1350 section 4;
  per the Context we match curl, so criterion 5's "the transfer continues with the
  learned endpoint" is false and must be corrected.

### The rules behind these numbers (curl 8.21.0 `lib/tftp.c`)

- `tftp_set_timeouts` runs once at connect and again when the first DATA/OACK arrives:
  `timeout` = time left in seconds (rounded) if 0 < left < 3600 s, else 15;
  `retry_max` = clamp(timeout / 5, 3, 50); `retry_time` = max(1, timeout / retry_max).
  At connect the time left is the connect timeout (300 s default, so 50 and 6); after
  the first packet there is no connect timeout left, so 15 gives 3 and 5 unless
  `--max-time` is set.
- The RRQ's `timeout` option is `retry_time`.
- A resend fires when `time(NULL) > rx_time + retry_time` in whole seconds, so the
  observed interval is `retry_time + 1` s (7 s and 4 s before the first packet, 6 s
  after it).
- RRQ phase: the first send counts as retry 1; when retries exceed `retry_max` the
  result is `TFTP_ERR_NORESPONSE`, exit 7. Data phase: re-ACK up to `retry_max` times,
  then `TFTP_ERR_TIMEOUT`, exit 28. `Curl_timeleft_ms` < 0 at any point is exit 28.
- The first datagram received pins the remote address; any later datagram from another
  address is `CURLE_RECV_ERROR` "Data received from another address".

### Re-planned 2026-09-26

The contract change is split out as BL-123 (`ConnectTimeout` and `MaxTime` on
`ITransferContext`), which this task now depends on; BL-122 and BL-124 parse and wire the
options in the CLI and console and are not needed here. The Goal and criteria are
corrected to the measurements above: exit 7 when the RRQ goes unanswered, exit 28 only in
the data phase, and exit 56 (not ERROR 5 and carry on) for a stranger. The one behaviour
not yet measured, `--max-time`, has its own measure-then-match criterion.

### Measured `--max-time` with curl 8.21.0 (2026-09-26, lane 1)

`curl -m 5 tftp://127.0.0.1:<port>/file.txt` against a silent loopback Python UDP server:
RRQ carries `timeout 1` (`tsize 0`, `blksize 512`); three byte-identical RRQs from one
source port at t = 0, 2.01, 4.04 s; then exit **28** with
`curl: (28) Operation timed out after 5008 milliseconds with 0 bytes received` at 5.1 s.
This is the rules above with 5 s left (retry_max 3, retry_time 1, interval 2 s) plus the
generic max-time check; the test on the fake clock asserts
`Operation timed out after 5000 milliseconds with 0 bytes received` at t = 5 s.

### Delivered 2026-09-26 (lane 1)

- `TftpRetrySchedule` derives `RetryLimit`/`RetrySeconds` from the time left; the RRQ's
  `timeout` option is `RetrySeconds`. `TftpDownload` waits with a
  `CancellationTokenSource(delay, TimeProvider)` linked to the transfer token, until the
  next re-send or `MaxTime`, whichever is sooner. The start timestamp is taken in
  `TftpProtocolHandler.ExecuteAsync` before the channel opens, since curl counts
  `--max-time` from the start of the transfer.
- Tests: `TftpRetransmissionTests` (10 tests) on the new fakes `ManualTimeProvider` and
  `FallsSilentDatagramChannel`. Tftp tests 51 -> 61; library at 100% line and branch.
- Choices taken (sensible defaults, no Stewart decision needed):
  - `ConnectTimeout` or `MaxTime` of zero or less means no limit, as curl treats
    `--connect-timeout 0` and `-m 0`.
  - A repeated last block is re-ACKed and restarts the resend wait (curl sets `rx_time`)
    but does not reset the retry count; a short repeat ends the transfer as curl's
    `TFTP_STATE_FIN` check does.
  - The first datagram of any kind, even one too short to parse, pins the server
    endpoint, because curl pins in `recvfrom` before reading the packet.
  - `MaxTime` in the data phase re-derives the schedule from what it leaves (20 s left
    gives 4 retries 6 s apart); a test pins it. Time left of an hour or more uses 15 s.
  - The MaxTime message always uses the `with N bytes received` form; curl's
    `with N out of M bytes received` form (when a size is known) does not arise because
    the download does not read `tsize` from an OACK.
  - Upload is unchanged: its write request still sends `timeout 6` and it does not
    re-send. Filed as BL-127.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Needs --connect-timeout (and --max-time) on ITransferContext in Curl.Protocol.Abstractions.UnitLibrary plus CLI wiring, outside touches; criteria 2 and 5 also contradict measured curl (exit 7, and exit 56 on a stranger). Re-plan; see Notes.
- 2026-09-26: Blocked -> Backlog. Re-planned: contract prerequisite split out as BL-123 (now a dependency); criteria corrected to measured curl 8.21.0 (exit 7 unanswered RRQ, 28 data-phase silence, 56 stranger) plus a measure-then-match --max-time criterion.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TFTP download re-sends to a silent server on curl 8.21.0's schedule and ends with exit 7, 28 or 56 as curl does; --max-time measured and matched
