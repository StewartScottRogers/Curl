---
id: BL-870
title: Dial with --keepalive-time and --keepalive-cnt in Curl.Console's TcpDialer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-645]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-870 — Dial with --keepalive-time and --keepalive-cnt in Curl.Console's TcpDialer

## Goal

`curl --keepalive-time 5 --keepalive-cnt 3 URL` dials with a keepalive idle time and interval of 5 seconds and 3 probes, because `Curl.Console` builds its `TcpDialer` from the parsed values.

## Context

- BL-645 parses `--keepalive-time` into `CommandLineOptions.TcpKeepAliveSeconds` and `--keepalive-cnt` into `CommandLineOptions.TcpKeepAliveProbeCount` (0 when not given), and adds `TcpSocketOptions.FromCommandLine(noDelay, keepAlive, keepAliveSeconds, keepAliveProbeCount)`, which maps 0 to libcurl's 60 seconds and 9 probes and clamps to `int.MaxValue`.
- Split from BL-645 because `Curl.Console` was held by BL-603 in `Doing` at the time.
- The one line to change is `CurlComposition.cs`, where `new TcpDialer(new TcpSocketOptions(options.TcpNoDelay, options.TcpKeepAlive))` is built.

## Acceptance criteria

- [ ] `CurlComposition` builds its `TcpDialer` with `TcpSocketOptions.FromCommandLine(options.TcpNoDelay, options.TcpKeepAlive, options.TcpKeepAliveSeconds, options.TcpKeepAliveProbeCount)`.
- [ ] A `CurlTransportsTests` test pins `--keepalive-time 5 --keepalive-cnt 3` as `new TcpSocketOptions(KeepAliveSeconds: 5, KeepAliveProbeCount: 3)` on `transports.TcpDialer.SocketOptions`, and the existing no-switch test still expects the defaults.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
