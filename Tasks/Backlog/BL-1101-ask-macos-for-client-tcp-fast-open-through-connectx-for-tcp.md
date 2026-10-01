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
completed:
---
# BL-1101 — Ask macOS for client TCP Fast Open through connectx for --tcp-fastopen

## Goal

On macOS, `--tcp-fastopen` connects through `connectx` with `CONNECT_DATA_IDEMPOTENT | CONNECT_RESUME_ON_READ_WRITE`, as libcurl 8.21.0's `cf-socket.c` does there, so the first request rides in the SYN.

## Context

- BL-647 / ADR-0317 set the raw `TCP_FASTOPEN` (`0x105`) option on macOS, the step .NET's `Socket` allows; client Fast Open proper on Darwin is `connectx`, which the BCL does not wrap.
- Start in `Curl.Networking.UnitLibrary/TcpDialer.cs` (`DialBoundAsync`) and `FastOpenSocketOption.cs`. A `[LibraryImport("libc")]` call to `connectx` on the socket's handle, guarded by `OperatingSystem.IsMacOS()`, is AOT-safe; keep the decision of when to use it in covered code and the call itself behind an ADR-0083 exclusion.
- Measure curl 8.21.0 on macOS (`-v --tcp-fastopen` against a loopback URL) before pinning any output.

## Acceptance criteria

- [ ] On macOS, a dial with `TcpSocketOptions.FastOpen` connects through `connectx`; an `OSCondition(OperatingSystems.OSX)` test pins it, and the choice of route is unit tested on every platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for Curl.Networking.UnitLibrary.

## Notes

## Log

- 2026-10-01: Created.
