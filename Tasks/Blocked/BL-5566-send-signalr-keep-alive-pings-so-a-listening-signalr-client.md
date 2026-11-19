---
id: BL-5566
title: Send SignalR keep-alive pings so a listening signalr client is not closed by the hub after 30 s
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client]
requirement: none
created: 2026-11-19
completed:
---
# BL-5566 — Send SignalR keep-alive pings so a listening signalr client is not closed by the hub after 30 s

## Goal

A `signalr://` client that only listens (no `-d`) keeps its hub connection open past 30 seconds, because it sends keep-alive pings as the SignalR protocol asks of clients.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 3 (Low). Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f`. The *Surly* recipe `signalr-receive-a-message` has a case showing it, with the exact command in its `review.md`.

The client sends no keep-alive, so a listener with no `-d` is closed by the hub after 30 seconds and exits 22.

**Spec:** the ASP.NET Core SignalR hub protocol (`HubProtocol.md`, Ping message, type 6). A client sends a Ping when it has sent nothing for its keep-alive interval (15 seconds by default) and the server closes a client it has not heard from within its client timeout (30 seconds by default). Check the current spec for both defaults and for how a client learns the server's values, and follow it.

**Where to start:** `Curl.Extended.Protocol.SignalR.UnitLibrary/Client`, and `HubProtocol/HubMessageWriter.cs` for writing the Ping. Use an injected `TimeProvider` for the interval, never `Thread.Sleep`. Decide whether an option changes the interval; if so, `--ai-help` and `--extended-help` describe it, and an ADR records the choice (decided by Claude under Stewart's delegation).

## Acceptance criteria

- [x] With a fake `TimeProvider`, a listening client with nothing to send writes a Ping message (`{"type":6}` plus the record separator) each keep-alive interval, and none while it is sending other messages within the interval.
- [ ] Against Surl's SignalR hub, a listener with no `-d` stays connected for at least 90 seconds and receives a message sent after 60 seconds; the Notes record the run.
- [x] Focused unit tests in `Curl.Extended.Protocol.SignalR.UnitTests` pin the ping timing.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Done: `SignalRHubSession` runs a keep-alive loop beside the read loop: a Ping (`{"type":6}` + 0x1E) whenever nothing was sent for 15 s (the .NET client default; the handshake does not carry the server values, and the 30 s server timeout is twice it). Any send (handshake, call, refusal, Ping) resets the idle clock. `SignalRProtocolHandler` takes an optional `TimeProvider`; `SignalRWebSocketSession` serialises frame writes with a semaphore. No option changes the interval, so no ADR.
- Tests: `SignalRKeepAliveTests` (manual clock). Measure-CodeQuality for the library: 100% line, 100% branch, worst CRAP 10. Fast tests 161 pass.
- Not done: criterion 2 (90 s run against Surl's hub) needs a Surl server, which no lane has. Run `curl signalr://<surl-host>/<hub>` with no `-d` for 90+ s, send a message at 60 s, then tick the box and move to Done.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 3.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 90934fec 32ee91d2 a766e51a 3d836d5e.
- 2026-11-19: Doing -> Blocked. Stewart: run a 90 s signalr:// listener against Surl's SignalR hub (message at 60 s) and tick criterion 2; code, tests and gates are done and committed
