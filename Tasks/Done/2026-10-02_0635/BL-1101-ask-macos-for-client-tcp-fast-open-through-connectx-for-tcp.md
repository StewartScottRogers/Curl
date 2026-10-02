---
id: BL-1101
title: Ask macOS for client TCP Fast Open through connectx for --tcp-fastopen
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1101 — Ask macOS for client TCP Fast Open through connectx for --tcp-fastopen

## Goal

On macOS, `--tcp-fastopen` connects through `connectx` with `CONNECT_DATA_IDEMPOTENT | CONNECT_RESUME_ON_READ_WRITE`, as libcurl 8.21.0's `cf-socket.c` does there, so the first request rides in the SYN.

## Context

- BL-647 / ADR-0317 set the raw `TCP_FASTOPEN` (`0x105`) option on macOS, the step .NET's `Socket` allows; client Fast Open proper on Darwin is `connectx`, which the BCL does not wrap.
- Start in `Curl.Networking.UnitLibrary/TcpDialer.cs` (`DialBoundAsync`) and `FastOpenSocketOption.cs`. A `[LibraryImport("libc")]` call to `connectx` on the socket's handle, guarded by `OperatingSystem.IsMacOS()`, is AOT-safe; keep the decision of when to use it in covered code and the call itself behind an ADR-0083 exclusion.
- Measure curl 8.21.0 on macOS (`-v --tcp-fastopen` against a loopback URL) before pinning any output.

## Acceptance criteria

- [x] On macOS, a dial with `TcpSocketOptions.FastOpen` connects through `connectx`; an `OSCondition(OperatingSystems.OSX)` test pins it, and the choice of route is unit tested on every platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Networking.UnitLibrary.

## Notes

- Plan (ADR-0355): `FastOpenSocketOption.ConnectsThroughConnectx` decides the route (FastOpen on Darwin only; unit tested on every platform). `DarwinFastOpenConnect.TryConnect` calls `connectx(CONNECT_RESUME_ON_READ_WRITE | CONNECT_DATA_IDEMPOTENT)` through `[LibraryImport("libc")]`, then wraps a `dup` of the descriptor in a new `Socket` (made from a handle, it reads the peer name and knows it is connected, which `NetworkStream` needs) and disposes the original. `TcpDialer.ConnectAsync` uses it in `DialBoundAsync` and `DialDeviceBoundAsync`, falling back to `Socket.ConnectAsync` when it answers null.
- Choice: a refused `connectx` falls back to a plain connect, so errors still come through .NET's exceptions; the raw `TCP_FASTOPEN` (0x105) of ADR-0317 stays set on macOS (harmless, keeps its tests).
- Choice: the macOS loopback test (`TryConnect_OnMacOS_ConnectsThroughConnectxAndCarriesTheFirstBytes`) is in the fast run, not `Integration`: CI runs only the fast tests, and its macOS job is the only place the route can be pinned. Loopback only.
- Not measured: curl 8.21.0 on macOS, as the lane runs on Windows. libcurl writes no `-v` line about the route, so no output is pinned. If CI macOS fails that test, the likely cause is the managed `Socket` not seeing the deferred connect as connected (`getpeername`).
- `Curl.Networking.UnitLibrary` now sets `AllowUnsafeBlocks` for the generated marshalling, as `Curl.Protocol.Ssh.UnitLibrary` does; the class is excluded from coverage at class level so the generated stubs are too. Measure-CodeQuality: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. On macOS --tcp-fastopen connects through connectx with CONNECT_DATA_IDEMPOTENT, as libcurl does (ADR-0355)
