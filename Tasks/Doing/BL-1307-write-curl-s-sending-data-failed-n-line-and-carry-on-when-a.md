---
id: BL-1307
title: Write curl's Sending data failed (N) line and carry on when a telnet negotiation reply cannot be sent
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1306]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1307 — Write curl's Sending data failed (N) line and carry on when a telnet negotiation reply cannot be sent

## Goal

When a telnet option negotiation reply (`WILL`/`WONT`/`DO`/`DONT`, or the NAWS subnegotiation) cannot be sent, Curl writes curl 8.21.0's `Sending data failed (<socket error number>)` info line where curl writes it (before a negotiation reply's `SENT ...` line, after the NAWS suboption's lines) and goes on with the session, instead of ending it with exit 55.

## Context

- curl 8.21.0 `lib/telnet.c` (tag `curl-8_21_0`):
  - `send_negotiation`, lines 225-240: each reply is its own 3-byte `swrite`; when it fails, `failf(data, "Sending data failed (%d)", SOCKERRNO)` and then `printoption(data, "SENT", cmd, option)` regardless; nothing is returned, so the session carries on.
  - `sendsuboption`, lines 671-716 (`printsub` at 702, the writes at 706-716): the NAWS subnegotiation's header and footer writes fail the same way, with the same line.
  - `failf` writes the line under `-v`; with exit 0 the text never reaches `curl: (N)`.
- Measured on 2026-10-02 against curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Response '\xff\xfd\x18\xff\xfb\x01hello'` (the server sends `DO TERM-TYPE`, `WILL ECHO` and `hello`, then closes, so curl's second write meets a closed socket) and `-sv telnet://127.0.0.1:PORT`: exit 0, stdout `hello`, stderr
  ```
  * RCVD DO TERM TYPE
  * SENT WONT TERM TYPE
  * RCVD WILL ECHO
  * Sending data failed (10053)
  * SENT DO ECHO
  { [5 bytes data]
  * Sending data failed (10053)
  * SENT WILL BINARY
  * Sending data failed (10053)
  * SENT DO BINARY
  * Sending data failed (10053)
  * SENT WILL SUPPRESS GO AHEAD
  * Sending data failed (10053)
  * SENT DO SUPPRESS GO AHEAD
  * shutting down connection #0
  ```
  10053 is `WSAECONNABORTED`; on Linux and macOS `SOCKERRNO` is the errno (`EPIPE`, `ECONNRESET`, ...).
- Curl today, `Curl.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs`: `ReceiveUntilClosedAsync` gathers every reply a read produced into `replies` and sends them in one `TrySendAsync`; a failed send returns `SendFailure` (exit 55, `Send failure: Connection was reset`). The `SENT` lines are written by `TelnetTraceReporter` as the replies are produced. In the measured run Curl's one batched write succeeded, so it printed no failure, but the same session with a failing write ends with exit 55 where curl exits 0.
- The number is the `SocketException` inside the connection's `IOException` (`NativeErrorCode`, which is the WSA code on Windows and the errno elsewhere, as `SOCKERRNO` is). An `IOException` without a `SocketException` inside keeps today's exit 55 path.
- The upload path (`SendUploadAsync`, curl's `send_telnet_data`) is not part of this task: curl does end the session there with `CURLE_SEND_ERROR`.

## Acceptance criteria

- [ ] Negotiation replies are sent one write each, in the order curl sends them; a test asserts the writes a fake connection records for the measured greeting are `FF FC 18` then `FF FD 01` (and the initial `WILL`/`DO` offers each in its own write).
- [ ] A test whose fake connection fails every write after the first with `IOException` wrapping `new SocketException((int)SocketError.ConnectionAborted)` asserts the info lines of the measured run in the measured order, with the number printed being that exception's `NativeErrorCode`, output `hello`, and exit 0.
- [ ] A test pins the NAWS case: with `TelnetOptions` holding `NAWS=80x24` and a server that sends `IAC DO NAWS`, a failing subnegotiation write writes `Sending data failed (N)` after the `SENT IAC SB NAWS` lines `printsub` writes (curl prints the suboption before its writes) and the session goes on.
- [ ] A test pins that a failing upload write still ends the session with exit 55 as today.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Telnet.UnitLibrary` reports no failing member.

## Notes

- curl's `failf` also fills the error buffer the first time, so a session that later fails for another reason prints `curl: (N) Sending data failed (10053)` instead of its own text. Not pinned here; file a follow-up if a measurement shows it matters.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
