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
completed:
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

- [ ] `TelnetProtocolHandler(IConnector connector)` implements `IProtocolHandler` with
      `SupportedSchemes` exactly `["telnet"]`, connects to
      `ConnectTarget(host, 23, false)` by default, and constructs no `Socket`.
- [ ] Each of the first three measured cases in `Context` is a named test against a
      scripted fake `IConnection` declared in `Curl.Protocol.Telnet.UnitTests`,
      asserting the exact bytes sent, the exact bytes written to `Output`, and exit 0.
- [ ] A test asserts a command sequence split across two reads (`FF` at the end of one
      read, `FB 01` at the start of the next) is still removed and answered.
- [ ] curl's own negotiation offers are measured against curl 8.21.0 during the run, the
      captures recorded in `Notes`, and each observed sequence pinned by a named test.
- [ ] A test asserts a failed `ConnectResult` is returned unchanged with nothing written.
- [ ] Every test builds its context with `TransferContext`; no `ITransferContext`
      implementation is declared in the test project, and no test is tagged
      `Integration`.
- [ ] `Curl.Protocol.Telnet.UnitLibrary/CLAUDE.md` names `IConnector` (ADR-0005) as the
      seam.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if a telnet row there is wrong, file
a task.

## Log

- 2026-09-26: Created.
