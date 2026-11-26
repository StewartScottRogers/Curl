---
id: BL-6036
title: Print -v's Connection timed out after line when -m ends a connect on Linux
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpConnectorTimedOutLineTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6036 — Print -v's Connection timed out after line when -m ends a connect on Linux

## Goal

On Linux, `curl -sv -m 3 http://192.0.2.20:1/` prints `* Connection timed out after N milliseconds` between `Trying` and `closing connection #0`, exit 28, as curl 8.18.0 does.

## Context

Seen in BL-6015 on 2026-11-26 with a linux-x64 self-contained publish of Curl.Console run in WSL Ubuntu: exit 28, but no timed out line, with and without `--doh-url`. Ubuntu curl 8.18.0 prints it, and so does Curl built for Windows. ADR-0117 says `TcpConnector` reports the line; the likely start is a race between the `-m` limit and the connect limit.

## Acceptance criteria

- [ ] A unit test pins that a connect ended by the `-m` limit reports `Connection timed out after N milliseconds`, whichever of the two limits fires first.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
