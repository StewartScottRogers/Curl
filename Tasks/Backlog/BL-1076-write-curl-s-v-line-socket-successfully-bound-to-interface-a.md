---
id: BL-1076
title: Write curl's -v line 'socket successfully bound to interface' after a device bind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1026]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1076 — Write curl's -v line 'socket successfully bound to interface' after a device bind

## Goal

On Linux, a plain `--interface <name>` or `if!<name>` whose `SO_BINDTODEVICE` succeeds writes the `-v` line `socket successfully bound to interface '<name>'` right after `Trying <ip>:<port>...`, as curl does.

## Context

- Follow-up from BL-1026 (ADR-0293, Consequences). `TcpDialer.DialFromDeviceAsync` binds the device but
  `ITcpDialer` has no way to tell `TcpConnector` it did, and the line must appear even when the connect
  then fails (measured: curl 8.18.0 on WSL, `--interface lo http://127.0.0.1:1/` prints it before
  `connect to 127.0.0.1 port 1 from 127.0.0.1 port N failed: Connection refused`).
- `ifhost!` writes no such line even when the device bind succeeds (measured).
- libcurl 8.21.0 writes it from `bindlocal` in `lib/cf-socket.c` through `infof`.

## Acceptance criteria

- [ ] A `Curl.Networking.UnitTests` test pins the line, after the `Trying` line, for a plain and an `if!` name whose device bind succeeds, and its absence for `ifhost!` and for a refused device bind.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
