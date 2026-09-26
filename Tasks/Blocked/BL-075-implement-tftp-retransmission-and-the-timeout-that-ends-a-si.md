---
id: BL-075
title: Implement TFTP retransmission and the timeout that ends a silent transfer
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-045]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-075 — Implement TFTP retransmission and the timeout that ends a silent transfer

## Goal

A TFTP download whose server goes silent re-sends its last packet on curl's schedule and
then ends with `CurlExitCode.OperationTimedOut` (28) and `Timeout was reached` at the
deadline curl uses, instead of waiting on `ReceiveAsync` forever.

## Context

- Builds on BL-045. Today `Curl.Protocol.Tftp.UnitLibrary/TftpDownload.cs` waits on
  `IDatagramChannel.ReceiveAsync` with no timeout, so a silent server hangs the transfer.
- Seam: `IDatagramChannel` / `IDatagramConnector` (ADR-0005,
  `Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`).
  Every wait goes through `ITransferContext.TimeProvider`
  (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`): no `Thread.Sleep`, no
  real delays in tests. Add a hand-written fake `TimeProvider` under
  `Curl.Protocol.Tftp.UnitTests/Fakes/` beside `ScriptedDatagramChannel.cs` and
  `RecordingDatagramConnector.cs` (no mocking package; MSTest only).
- Exit code: `CurlExitCode.OperationTimedOut` = 28 in
  `Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`.

Measured 2026-09-26 with local curl 8.21.0 (Release-Date 2026-06-24) against loopback
UDP servers written in Python:

1. **Silent server, no options** (`curl tftp://127.0.0.1:16901/file.txt`): curl
   re-sent the identical RRQ (`tsize 0`, `blksize 512`, `timeout 6`) from the same source
   port at t = 0, 7.08, 14.12, 21.21 s, ... about every 7.05 s, and was still sending at
   239 s when the measurement was stopped. The default overall give-up time and the
   final exit code and message were **not** reached and must still be measured. It is
   expected to be bounded by curl's default connect timeout of 300 s, but measure it; do
   not assume.
2. **Server that answered the RRQ with one full 512-byte DATA block 1 from a new port
   and then went silent**, run with `--connect-timeout 10`: the RRQ's `timeout` option
   became `3` (not `6`); curl sent ACK 1 at 0 s, re-sent ACK 1 to the transfer port at
   6.06, 12.03 and 18.08 s, and ended at about 25 s with exit 28 and message
   `Timeout was reached`.
3. **Not yet handled by BL-045 and in scope here, still to be measured with curl 8.21.0:**
   - a duplicate DATA block (curl is expected to re-ACK the last block; confirm);
   - a datagram from an endpoint other than the learned transfer identifier. RFC 1350
     section 4 says reply ERROR 5 (unknown transfer ID) to the stranger and carry on;
     record what curl actually does and match curl.

   If a measurement shows curl diverging from the RFC, match curl and note it; if
   matching curl is not possible, stop and file a Stewart decision task rather than
   diverging silently.

## Acceptance criteria

- [ ] `Notes` records, measured with curl 8.21.0: the default give-up time for a silent
      server with no options, and the exit code and message it ends with; how curl
      handles a duplicate DATA block; and how it handles a datagram from an unknown
      endpoint.
- [ ] A test in `Curl.Protocol.Tftp.UnitTests` driving a fake `TimeProvider` asserts the
      RRQ is re-sent, byte-identical, at the measured interval while the server is silent,
      and that the transfer ends at the measured default deadline with the measured exit
      code and message.
- [ ] A test asserts that with `--connect-timeout 10` the RRQ carries `timeout 3`, ACK 1 is
      re-sent to the transfer endpoint (not the initial server port) at the measured
      interval, and the transfer ends with `CurlExitCode.OperationTimedOut` (28) and
      `Timeout was reached` at the measured deadline.
- [ ] A test asserts duplicate-DATA handling matches the measured curl behaviour.
- [ ] A test asserts unknown-endpoint datagram handling matches the measured curl
      behaviour, and the transfer continues with the learned endpoint.
- [ ] No test uses `Thread.Sleep`, `Task.Delay` on the real clock, or
      `TestCategory=Integration`; every wait in `TftpDownload` goes through
      `ITransferContext.TimeProvider`.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` and
      `dotnet build Curl.Protocol.Tftp.UnitTests -warnaserror` are clean, and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `Curl.Protocol.Tftp.UnitLibrary` has 100% line and 100% branch coverage.

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

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Blocked. Needs --connect-timeout (and --max-time) on ITransferContext in Curl.Protocol.Abstractions.UnitLibrary plus CLI wiring, outside touches; criteria 2 and 5 also contradict measured curl (exit 7, and exit 56 on a stranger). Re-plan; see Notes.
