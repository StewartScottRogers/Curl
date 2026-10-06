---
id: BL-044
title: Honour -t/--telnet-option TTYPE, XDISPLOC and NEW_ENV
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-043]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-044 — Honour -t/--telnet-option TTYPE, XDISPLOC and NEW_ENV

## Goal

The telnet handler honours `-t`/`--telnet-option` (`ITransferContext.TelnetOptions`) for
`TTYPE`, `XDISPLOC` and `NEW_ENV`, and refuses an unknown or malformed option with curl
8.21.0's exit codes and messages.

## Context

Builds on the telnet handler from BL-043. Upstream: "libcurl supports the options TTYPE,
XDISPLOC and NEW_ENV", given as `<option=value>`
(<https://curl.se/libcurl/c/CURLOPT_TELNETOPTIONS.html>, checked 2026-09-26); RFC 1091
(TTYPE), RFC 1096 (XDISPLOC) and RFC 1572 (NEW-ENVIRON).

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback listener:

- `-t ttype=vt100`, server sends `FF FD 18` (`IAC DO TTYPE`) then
  `FF FA 18 01 FF F0` (`IAC SB TTYPE SEND IAC SE`): curl sent
  `FF FB 18 FF FB 00 FF FD 00 FF FB 03 FF FD 03 FF FA 18 00 76 74 31 30 30 FF F0`,
  that is `IAC WILL TTYPE`, the four BINARY/SGA offers, then
  `IAC SB TTYPE IS "vt100" IAC SE`. The option name matched ignoring case.
- `-t BOGUS=1`: exit 48 (`CURLE_UNKNOWN_OPTION`), stderr
  `curl: (48) An unknown option was passed in to libcurl`, after the TCP connection was
  made and before any byte was sent.
- `-t TTYPE` (no `=`): exit 49 (`CURLE_SETOPT_OPTION_SYNTAX`), stderr
  `curl: (49) Syntax error in telnet option: TTYPE`, likewise after connecting and before
  sending.
- With `-t TTYPE=vt100 -t XDISPLOC=host:0 -t NEW_ENV=USER,bob` and a server sending
  `IAC DO TTYPE`, `IAC DO XDISPLOC` (`0x23`) and `IAC DO NEW-ENVIRON` (`0x27`), curl
  answered `IAC WILL` for all three. The subnegotiation replies for `XDISPLOC` and
  `NEW_ENV` were not captured (the server never sent `SB ... SEND` for them); measure
  them during the run.

Exit codes: <https://curl.se/libcurl/c/libcurl-errors.html>.

## Acceptance criteria

- [x] The first measured case is a named test asserting the exact bytes sent and exit 0.
- [x] Tests assert `-t BOGUS=1` returns `CurlExitCode.UnknownOption` with
      `ErrorMessage` `An unknown option was passed in to libcurl`, and `-t TTYPE` returns
      `CurlExitCode.SetoptOptionSyntax` with `Syntax error in telnet option: TTYPE`; in
      both the connector was asked to connect and nothing was written to the connection.
- [x] The `XDISPLOC` and `NEW_ENV` subnegotiation replies are measured against curl
      8.21.0 (a server sending `IAC SB XDISPLOC SEND IAC SE` and
      `IAC SB NEW-ENVIRON SEND IAC SE`), the captures recorded in `Notes`, and each
      pinned by a named test.
- [x] Without any `-t`, every BL-043 test still passes unchanged.
- [x] Every test builds its context with `TransferContext`; no test is tagged
      `Integration`.
- [x] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

### Plan (as built)

- `TelnetOptionParser` reads `ITransferContext.TelnetOptions` into `TelnetOptionValues`
  inside `ExecuteAsync`, after `IConnector.ConnectAsync` succeeds and before any byte is
  sent; a failure is returned as the `TransferResult` and the connection disposed.
- `TelnetReceiver` now takes the `TelnetOptionValues`: each of TTYPE (24), XDISPLOC (35)
  and NEW-ENVIRON (39) with a value joins the options this side performs (so `IAC DO x`
  gets `IAC WILL x`) and is offered after BINARY/SGA in option-number order. Their
  subnegotiations are answered with `IS` and the value; NEW-ENVIRON with the `VAR`/`VALUE`
  list.
- Tests: `TelnetProtocolHandlerTelnetOptionTests` (60 cases). Telnet test project 103
  passing; line and branch coverage of `Curl.Protocol.Telnet.UnitLibrary` 100%.

### Measurements, curl 8.21.0, loopback listener, 2026-09-26 (stdin empty)

Server bytes → bytes curl sent (`O` = `FF FB 00 FF FD 00 FF FB 03 FF FD 03`, the offers):

- `-t XDISPLOC=host:0`, `FF FD 23` then `FF FA 23 01 FF F0` →
  `FF FB 23 O FF FA 23 00 68 6F 73 74 3A 30 FF F0`, exit 0.
- `-t NEW_ENV=USER,bob`, `FF FD 27` then `FF FA 27 01 FF F0` →
  `FF FB 27 O FF FA 27 00 00 55 53 45 52 01 62 6F 62 FF F0`, exit 0.
- `-t NEW_ENV=USER,bob -t new_env=TERM -t NEW_ENV=A,b,c` → the IS list
  `00 55 53 45 52 01 62 6F 62 00 54 45 52 4D 00 41 01 62 2C 63`: split at the first comma,
  a variable without a comma is sent without `VALUE`.
- `-t NEW_ENV=,x -t NEW_ENV= -t NEW_ENV=a,` → `FF FA 27 00 00 01 78 00 00 61 01 FF F0`.
- All three options, `FF FD 18 FF FD 23 FF FD 27` then the three `SB ... SEND`s →
  `FF FB 18 FF FB 23 FF FB 27 O` then the three `IS` replies in order.
- All three options, server sends only `FF FB 01` → `FF FD 01 O FF FB 18 FF FB 23 FF FB 27`.
- `-t TTYPE=` → `FF FA 18 00 FF F0`. `-t TTYPE=x` answers `FF FA 18 00 FF F0` and
  `FF FA 18 FF F0` too (the qualifier is not checked); `-t NEW_ENV=x` answers
  `FF FA 27 02 FF F0`; `FF FA 05 FF F0` is ignored.
- TTYPE or XDISPLOC of 1000 characters is sent; 1001 → exit 55 `Too long telnet TTYPE`
  / `Too long telnet XDISPLOC`, nothing sent.
- NEW-ENVIRON size: one variable of 2036 characters is sent (reply 2043 bytes), 2037 is
  left out (`FF FA 27 00 FF F0`). With 1000 + 1035 + `c`, `c` is left out (2043 bytes);
  with 1000 + 1036 + `c`, the second is left out and `c` sent (1009 bytes). Rule: add a
  variable while (reply length so far + its length + 1) < 2042.
- Refusals, all after connecting and with nothing sent:
  - no `=` → exit 49 `Syntax error in telnet option: <option>` (`TTYPE`, `BINARY`).
  - an unknown name whose UTF-8 length is 2, 5, 6, 7 or 8 (`TTYPX`, `WX`, `BINARZ`,
    `NEW_ENX`, `XDISPLOX`, `BOGUS`) → exit 48 `An unknown option was passed in to
    libcurl`; any other length (`BOG`, `BOGU`, `BOGUSXYZZ`, empty) → exit 48
    `Unknown telnet option <option>`. Non-ASCII names confirmed this counts UTF-8 bytes.
  - `WS=`: `80x24`, `ws=80x24`, `80x24z`, `080x024`, `65535x65535`, `80x0`, `0x24` exit 0;
    `80`, `80x`, `x24`, `80X24`, `80y24`, `70000x24`, `65536x1`, `80x70000`, `80x-1`,
    ` +80x 24`, `99999999999999999999999x1` → exit 49.
  - `BINARY=0`, `BINARY=x`, `BINARY=`, `binary=1` exit 0.
  - The first bad option decides: `TTYPE=vt100 BOGUS=1 TTYPE` → 48; `TTYPE BOGUS=1` → 49.
- A value with any non-ASCII character skips the option unread: `BOGUS=é` exit 0;
  `ttype=é` then `SB TTYPE SEND` → exit 43.

### Choices made unattended

- `WS` and `BINARY` are accepted and checked exactly as measured but otherwise ignored,
  so a script passing them is not refused with a 48 curl never gives. Negotiating them is
  filed as BL-085 (NAWS) and BL-083 (BINARY=0).
- curl also sends the `-u` user name as `NEW-ENVIRON USER`; not in this task's goal, filed
  as BL-084.
- New tests live in their own class, `TelnetProtocolHandlerTelnetOptionTests`, beside
  `TelnetProtocolHandlerTests`, so BL-043's tests stay unchanged.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. telnet honours -t TTYPE, XDISPLOC and NEW_ENV byte for byte as curl 8.21.0, and refuses bad options with exit 48/49 after connecting
