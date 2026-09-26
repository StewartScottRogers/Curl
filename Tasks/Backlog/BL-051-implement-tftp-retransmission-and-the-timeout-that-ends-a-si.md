---
id: BL-051
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
# BL-051 — Implement TFTP retransmission and the timeout that ends a silent transfer

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

## Log

- 2026-09-26: Created.
