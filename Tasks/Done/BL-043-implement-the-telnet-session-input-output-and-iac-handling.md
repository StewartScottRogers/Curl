---
id: BL-043
title: Implement the telnet session - input, output and IAC handling
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-033, BL-034, BL-035]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-043 — Implement the telnet session - input, output and IAC handling

## Goal

`TelnetProtocolHandler` in `Curl.Protocol.Telnet.UnitLibrary` serves `telnet`: it sends
the `Upload` bytes unchanged, writes received data to `Output` with telnet command
sequences removed, answers the server's option negotiation as curl 8.21.0 does, and ends
with exit 0 when the server closes.

## Context

Requirements: the `telnet://` rows BL-033 adds to `Documentation/Product/Requirements.md`.
Seams: ADR-0005 (`IConnector`) and ADR-0006 (`TransferContext`; standard input arrives
as `ITransferContext.Upload`, supplied by whoever composes the transfer). Protocol:
RFC 854 (commands, `IAC` = `0xFF`) and RFC 855 (option negotiation). Upstream: "Fetching
a telnet URL starts an interactive session where it sends what it reads on stdin and
outputs what the server sends it" (<https://curl.se/docs/manpage.html>, checked
2026-09-26); default port 23 (<https://curl.se/docs/url-syntax.html>).

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener:

- stdin `a\nb\n`, server sends `hi\r\n` and closes: curl sent `a\nb\n` unchanged (no
  CRLF conversion), wrote `hi\r\n`, exit 0.
- Empty stdin, server sends `hi\r\n` and closes: curl sent nothing, wrote `hi\r\n`,
  exit 0.
- Server sends `FF FB 01` (`IAC WILL ECHO`) then `hi FF FF x\r\n`: curl sent
  `FF FD 01` (`IAC DO ECHO`) and wrote `hi\xFFx\r\n` - the negotiation removed, `IAC IAC`
  written as one `0xFF`.
- Server sends `IAC DO TTYPE`: in that capture curl also sent, unprompted,
  `IAC WILL BINARY`, `IAC DO BINARY`, `IAC WILL SGA`, `IAC DO SGA`; with only
  `IAC WILL ECHO` it sent none of those. What triggers curl's own offers is not settled
  by these captures; measure it during the run.

`-t` options (`TTYPE`, `XDISPLOC`, `NEW_ENV`) are the next task; this handler ignores
`TelnetOptions` for now and refuses every option it has no reason to accept, per
RFC 855, unless a measurement shows curl accepting it.

Reading from the connection and writing `Upload` run concurrently: the session ends when
the server closes, not when `Upload` is exhausted.

## Acceptance criteria

- [x] `TelnetProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["telnet"]`, connects to
      `ConnectTarget(host, 23, false)` by default, and constructs no `Socket`.
- [x] Each of the first three measured cases in `Context` is a named test against a
      scripted fake `IConnection` declared in `Curl.Protocol.Telnet.UnitTests`,
      asserting the exact bytes sent, the exact bytes written to `Output`, and exit 0.
- [x] A test asserts a command sequence split across two reads (`FF` at the end of one
      read, `FB 01` at the start of the next) is still removed and answered.
- [x] curl's own negotiation offers are measured against curl 8.21.0 during the run, the
      captures recorded in `Notes`, and each observed sequence pinned by a named test.
- [x] A test asserts a failed `ConnectResult` is returned unchanged with nothing written.
- [x] Every test builds its context with `TransferContext`; no `ITransferContext`
      implementation is declared in the test project, and no test is tagged
      `Integration`.
- [x] `Curl.Protocol.Telnet.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam.
- [x] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if a telnet row there is wrong, file
a task.

### Plan as built

`TelnetProtocolHandler` connects through `IConnector`, then runs two loops at once: the
upload pump (reads `Upload`, doubles `0xFF`, sends) and the receive loop (reads, feeds
`TelnetReceiver`, writes data to `Output`, sends replies). Sends share a `SemaphoreSlim`.
`TelnetReceiver` is a pure byte-level state machine copied from curl's `telrcv`;
`TelnetOptionSide` is RFC 1143 state for one side. Library at 100% line and 100% branch
coverage, 43 tests.

### Measurements (curl 8.21.0, loopback listener in Python, 2026-09-26)

Bytes hex; "offers" = `FF FB 00 FF FD 00 FF FB 03 FF FD 03` (WILL BINARY, DO BINARY,
WILL SGA, DO SGA).

- Server `FF FB 01 68 69 FF FF 78 0D 0A`: sent `FF FD 01` + offers; output
  `68 69 FF 78 0D 0A`; exit 0. **Corrects Context**: curl does send the offers after
  `IAC WILL ECHO`. The earlier capture likely closed before reading them; re-measured
  three ways (same read as data, alone, data in a later read) and all three sent them.
- What triggers the offers: the first read that contains any `WILL`, `WONT`, `DO` or
  `DONT`. They go once, after that read's replies, and leave out any option already
  settled. `SB`, `NOP`, `DM`, `GA` do not trigger them.
- Single commands (sent): `DO TTYPE` -> `FF FC 18` + offers; `WILL TTYPE` -> `FF FE 18`
  + offers; `DO ECHO` -> `FF FC 01` + offers; `WONT ECHO` -> offers; `DONT BINARY` ->
  offers; `WILL SGA` -> `FF FD 03 FF FB 00 FF FD 00 FF FB 03`; `DO SGA` ->
  `FF FB 03 FF FB 00 FF FD 00 FF FD 03`; `WILL BINARY` -> `FF FD 00 FF FB 00 FF FB 03 FF FD 03`;
  `DO BINARY` -> `FF FB 00` + the other three offers.
- `WILL ECHO` + `DO TTYPE` in one read -> `FF FD 01 FF FC 18` + offers.
  `WILL ECHO` twice -> `FF FD 01` + offers. `DO BIN, DONT BIN` -> `FF FB 00 FF FC 00` +
  offers. `WILL BIN, WONT BIN` -> `FF FD 00 FF FE 00` + offers.
- Across reads: `WILL ECHO`; then `DO BIN, DONT SGA, WILL BIN, WONT SGA` -> nothing (all
  answer our offers); then `DONT BIN, WONT BIN` -> `FF FC 00 FF FE 00`; then
  `WILL ECHO, DO BIN, WILL SGA, DO SGA` -> `FF FB 00 FF FD 03 FF FB 03`. This is RFC 1143
  with ECHO, BINARY, SGA accepted from the server and BINARY, SGA performed locally.
- Split `FF` | `FB 01 68 69` -> sent `FF FD 01` + offers, output `68 69`.
- Output: `61 FF F1 62 0D 00 63 0D 0A` -> `61 62 0D 63 0D 0A` (NOP removed, NUL after CR
  dropped); `IAC DM`, `IAC GA` removed; `0D FF FB 01` after `61` -> written as data
  `61 0D FF FB 01 62` (the byte after CR is always data); `61 0D FF FF 62` -> `61 0D FF`;
  `61 0D 0D 00 62` -> unchanged.
- Subnegotiation with no `-t`: `SB 05 01 SE` removed, nothing sent; empty `SB SE` and
  `IAC IAC` inside SB removed; `SB NEW-ENVIRON SEND SE` -> sent `FF FA 27 00 FF F0`, no
  offers; `SB TTYPE SEND SE` or `SB XDISPLOC SEND SE` -> exit 43,
  `A libcurl function was given a bad argument`, output up to the SB only, nothing
  sent; `SB 05 01 IAC WILL` -> exit 56, `telnet: suboption error`, nothing sent.
- Stdin `61 FF 62 0A` -> sent `61 FF FF 62 0A`. **Corrects Goal**: the upload is not sent
  wholly unchanged; `0xFF` is doubled. No CRLF conversion (stdin `a\nb\n` sent as is).

### Choices made unattended

- Implemented the no-`-t` subnegotiation answers (43, NEW-ENVIRON empty IS, 56) here
  rather than leave them to BL-044, because they are this handler's behaviour when
  `TelnetOptions` is empty and they were measured; BL-044 changes them when `-t` values
  exist.
- An upload still pending when the server closes is abandoned (cancelled, not awaited),
  so an idle console cannot hold the session open; its faults are observed. An upload
  read failure does not end the session. Connection and output I/O failures are not
  mapped to exit codes yet: filed as BL-051.
- The follow-up was filed with the board script directly rather than through
  `task-planner`, to keep the unattended run short.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet:// session sends stdin (IAC doubled), writes server data with commands removed, negotiates options as curl 8.21.0 does, exit 0 on close
