---
id: BL-2374
title: Fill CURLINFO_OS_ERRNO from the socket error of a failed connect
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2366]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2374 — Fill CURLINFO_OS_ERRNO from the socket error of a failed connect

## Goal

After `ManagedEasyTransfer.PerformAsync` fails to connect, `Info.OsErrno` holds the operating system's error number of the last failed connect attempt, as libcurl's `CURLINFO_OS_ERRNO` does, instead of 0.

## Context

Split from BL-2366, which filled the two TLS verify results. `TcpConnector.DialFailure` (Curl.Networking.UnitLibrary) already has the last attempt's `SocketError` but `ConnectResult` drops it. Carry it on `ConnectResult` (Curl.Protocol.Abstractions), record a failed connect's value in `Curl.Engine`'s `EndPointRecordingConnector` (or a sibling recorder), put it on `TransferReport.OsErrno` and read it in `TransferInfoFromReport`. The number is per platform: the Schannel build reports the Winsock code (`WSAECONNREFUSED` 10061 = `(int)SocketError.ConnectionRefused`); the OpenSSL builds report `errno` (`ECONNREFUSED` is 111 on Linux, 61 on macOS), so a `SocketError` -> errno table is needed off Windows. Measure real curl (`curl -w` has no variable for it; use `--retry-connrefused` behaviour or a libcurl measurement) before pinning values.

## Acceptance criteria

- [ ] `ConnectResult` of a refused dial carries the last attempt's socket error (a `TcpConnector` unit test).
- [ ] `TransferInfoFromReportTests` asserts `OsErrno` filled from a report that carries it, and 0 from one that does not.
- [ ] The errno of a refused connect is the platform's: 10061 on Windows, 111 on Linux, 61 on macOS (each pinned in its own OS-conditioned test).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
