---
id: BL-077
title: Map telnet connection and output failures to curl's exit codes
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-043]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-077 — Map telnet connection and output failures to curl's exit codes

## Goal

When a telnet session's connection read fails, a send to the server fails, or writing to
`Output` fails, `TelnetProtocolHandler` returns curl 8.21.0's exit code and message as a
`TransferResult` instead of letting the exception escape.

## Context

BL-043 left these paths unhandled: an `IOException` from `IConnection.ReadAsync`,
`IConnection.WriteAsync` or `Output.WriteAsync` propagates out of `ExecuteAsync`, and an
upload read failure is ignored (the session keeps receiving). Likely codes are exit 56
(`CURLE_RECV_ERROR`), 55 (`CURLE_SEND_ERROR`) and 23 (`CURLE_WRITE_ERROR`)
(<https://curl.se/libcurl/c/libcurl-errors.html>), but measure each against curl 8.21.0
with a loopback listener that resets the connection, and a closed stdout, before pinning
them. `IConnection`'s contract (ADR-0005) does not say which exception a failed read
throws; read `Curl.Networking.UnitLibrary/StreamConnection.cs` to see what production
throws.

## Acceptance criteria

- [ ] A connection read that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test with a fake `IConnection`.
- [ ] A send to the server that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test.
- [ ] An `Output` write that throws `IOException` returns curl's measured exit code and
      message, pinned by a named test.
- [ ] The measurements are recorded in `Notes`.
- [ ] `dotnet build Curl.Protocol.Telnet.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`
      is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
