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
completed:
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

- [ ] The first measured case is a named test asserting the exact bytes sent and exit 0.
- [ ] Tests assert `-t BOGUS=1` returns `CurlExitCode.UnknownOption` with
      `ErrorMessage` `An unknown option was passed in to libcurl`, and `-t TTYPE` returns
      `CurlExitCode.SetoptOptionSyntax` with `Syntax error in telnet option: TTYPE`; in
      both the connector was asked to connect and nothing was written to the connection.
- [ ] The `XDISPLOC` and `NEW_ENV` subnegotiation replies are measured against curl
      8.21.0 (a server sending `IAC SB XDISPLOC SEND IAC SE` and
      `IAC SB NEW-ENVIRON SEND IAC SE`), the captures recorded in `Notes`, and each
      pinned by a named test.
- [ ] Without any `-t`, every BL-043 test still passes unchanged.
- [ ] Every test builds its context with `TransferContext`; no test is tagged
      `Integration`.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-26: Created.
