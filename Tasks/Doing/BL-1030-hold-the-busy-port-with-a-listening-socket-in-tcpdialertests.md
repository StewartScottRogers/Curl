---
id: BL-1030
title: Hold the busy port with a listening socket in TcpDialerTests so the --local-port tests pass on Linux
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests/TcpDialerTests.cs]
requirement: none
created: 2026-09-29
completed:
---
# BL-1030 — Hold the busy port with a listening socket in TcpDialerTests so the --local-port tests pass on Linux

## Goal

`BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext` and `BindLocalEnd_WhenEveryPortIsInUse_ThrowsInterfaceFailed` pass on Windows, Linux and macOS.

## Context

Since BL-600 (commit 2c49fa2b) CI has failed on ubuntu-latest only, e.g. run 36674691490: the first test got the busy port instead of the next, and the second saw no exception. The tests held the "busy" port with a socket that was bound but not listening. On Linux .NET sets `SO_REUSEADDR` on TCP sockets, so a second bound-only socket can take the same port; Windows refuses. A listening holder is in use on every platform, and is what a busy port looks like in practice.

## Acceptance criteria

- [ ] Both port holders in `TcpDialerTests` call `Listen()` after `Bind`.
- [ ] The `CI` workflow passes on Windows, Linux and macOS for the commit that lands this.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
