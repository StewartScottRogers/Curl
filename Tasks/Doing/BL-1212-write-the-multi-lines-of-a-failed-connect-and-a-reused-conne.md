---
id: BL-1212
title: Write the [MULTI] lines of a failed connect and a reused connection for --trace-config multi
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1188]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1212 — Write the [MULTI] lines of a failed connect and a reused connection for --trace-config multi

## Goal

A refused connect and a transfer on a reused connection write curl 8.21.0's `[MULTI]` lines under `-v --trace-config multi`, as a plain HTTP transfer already does (BL-1188).

## Context

- BL-1188 (ADR-0382) added `Curl.Console`'s `MultiStateTraceEvents` for a plain HTTP transfer on a new connection. BL-1188 Notes hold the measured refused connect (`http://127.0.0.1:1/`): after `Trying`, the poll lines repeat, then `Curl_multi_will_close fd=N`, the `connect to ... failed` and `Failed to connect` lines, `failed to connect [0][!DNS][!SETUP][!HAPPY-EYEBALLS]`, `connect failed -> 7`, `multi_done: status: 7 prem: 1 done: 0`, `multi_done_locked, in use=0`, `multi_done, terminating conn #0 to 127.0.0.1:1, forbid=0, close=0, premature=1, conn_multiplex=0`, `closing connection #0`, `-> [COMPLETED]`, three `[COMPLETED] [PGRS-*] added` lines (PRETRANSFER, POSTRANSFER, STARTTRANSFER), `-> [MSGSENT]`, `removed from multi`. Today such a transfer writes the connect groups and no closing lines.
- A reused connection writes none of the connect groups today but still writes `reduced to [0][TCP]`, `-> [PROTOCONNECT]` and `[PROTOCONNECT] -> [DO]`, which curl does not write for a reused connection. Measure first: `Record-CurlExchange.ps1 -Connections 1` with two URLs and a keep-alive response (the BL-1188 two-URL measurement had its connection reset, so measure again).

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1` (refused connect, two URLs on one kept-alive connection); stderr in Notes.
- [ ] `MultiStateTraceEventsTests` pin the refused connect's and the reused connection's `[MULTI]` lines in curl's order.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
