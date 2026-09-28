---
id: BL-456
title: Add TcpConnectionListener and LocalEndPoint to Curl.Networking for FTP active mode
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-455]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-456 — Add TcpConnectionListener and LocalEndPoint to Curl.Networking for FTP active mode

## Goal

`Curl.Networking` has `TcpConnectionListener`, the production `IConnectionListener` of ADR-0101, and its TCP connections report `LocalEndPoint`, so an FTP transfer in active mode can listen on a port and accept the server's data connection.

## Context

- ADR-0101, "Contract additions", item 2. The contract comes from BL-455.
- `TcpConnector` in `Curl.Networking.UnitLibrary` shows how a socket becomes an `IConnection` and how failures become `ConnectResult.Failed` with curl's exit code and message.
- The listener binds the `ListenTarget` address, trying each port of its range in turn; a range with no free port fails. Take curl 8.21.0's exit code and message for a failed bind (`CURLE_FTP_PORT_FAILED`, 30) from a measurement with `Record-CurlExchange.ps1 -Ftp`, or from `lib/ftp.c` if the case cannot be provoked, and say which under Notes.
- Tests that open a loopback socket are fine as long as they stay fast and platform-neutral; mark any that cannot `TestCategory=Integration`.

## Acceptance criteria

- [ ] `TcpConnectionListener.ListenAsync` on `127.0.0.1` port 0 returns a pending connection whose `LocalEndPoint` has a non-zero port, and `AcceptAsync` returns a connection when a client connects to it; pinned by a named test.
- [ ] A port range whose every port is taken returns a failed `ListenResult` with the exit code and message recorded under Notes; pinned by a named test.
- [ ] The connections `TcpConnector` returns report a non-null `LocalEndPoint`; pinned by a named test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes on Windows, and no test is Windows-only without an `OSCondition`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for the new members.

## Notes

Filed by BL-437 under ADR-0101. BL-458 depends on this task.

## Log

- 2026-09-27: Created.
