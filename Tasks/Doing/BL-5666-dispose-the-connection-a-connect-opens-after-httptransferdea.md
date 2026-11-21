---
id: BL-5666
title: Dispose the connection a connect opens after HttpTransferDeadline's limit gave up on it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-21
completed:
---
# BL-5666 — Dispose the connection a connect opens after HttpTransferDeadline's limit gave up on it

## Goal

A connect that `HttpTransferDeadline` stops waiting for at the connect or transfer limit never leaves an open connection or an unobserved fault behind, and the comment says what the code does.

## Context

- `Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs:150-153` (`WithinConnectTimeoutAsync`), added for BL-5660. Found by the shift-end review's silent-failure-hunter and code-reviewer.
- `WaitAsync(limit.Token)` throws when `--connect-timeout` or the transfer token fires, and drops the inner `connectAsync` task. A connector that ignores its token (the BL-5660 case) can still finish later with a live `ConnectResult.Connection` (a socket, or a QUIC connection with its UDP socket). Nobody awaits that task, so the connection is never disposed and a late fault is never observed. In `curl.exe` process exit hides it; in the long-lived hosts (`Curl.Api`, `Libcurl`, `Curl.Mcp`) every such timeout leaks a connection.
- The new comment says "an abandoned connect is still awaited, so the race can dispose the connection it opens anyway". That holds for the `abandoned` token only, not for the limit `WaitAsync` adds.

## Acceptance criteria

- [x] When `WaitAsync` gives up at the limit, a continuation on the inner task disposes any connection it later returns and observes its exception.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` uses a fake connector that ignores its token and returns a connection after the connect timeout (on a fake `TimeProvider`), and asserts the transfer ends with exit 28 and the late connection is disposed.
- [x] The comment at the `WaitAsync` line describes both the abandoned case and the limit case truthfully.
- [x] `dotnet build` is clean and the fast tests pass; the library keeps 100% line and branch coverage.

## Notes

- Continuation is `HttpTransferDeadline.DisposeLateConnectionAsync` (disposes a late connection, swallows a late fault). Made `WithinConnectTimeoutAsync` non-generic (both callers use ConnectResult). Quality run: 0 failing members; fast tests green. Committed with the task.

## Log

- 2026-11-21: Created by the shift-end review (ADR-0710) of 8c81a2532a...1f330a1f28.
- 2026-11-21: Backlog -> Doing. Criteria at claim: a963e8df 8bf195a0 9b28f950 e7657cfb.
