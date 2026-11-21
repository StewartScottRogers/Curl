---
id: BL-5660
title: Stop the first Curl.Api HTTP/3 request in a process from losing the race to TCP or hanging past 60 s
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5660 — Stop the first Curl.Api HTTP/3 request in a process from losing the race to TCP or hanging past 60 s

## Goal

The first HTTP/3 request through `Curl.Api` in a fresh process (`Http3` or `Http3Only`) behaves like later ones: it races TCP as ADR-0144 describes and never hangs past its timeout.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 2. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The first QUIC request in a process lost the race to TCP, and once hung past 60 s; a warm-up `Http3Only` request on a separate client avoided it. One-time start-up cost (JIT of the hand-built QUIC and TLS 1.3 code, see BL-5635) probably makes the first QUIC attempt slow enough to lose the race, and something on that path (the hang) does not honour the request's timeout. Reproduce in a fresh process against a loopback HTTP/3 server: measure the first attempt's time per stage, make the race's head start (curl's 200 ms "happy eyeballs" delay for TCP after QUIC) start after QUIC set-up rather than before, and find what blocks past the timeout. Inject `TimeProvider` for the race delay.

## Acceptance criteria

- [ ] In a fresh process, the first `Curl.Api` `Http3` request to a loopback HTTP/3 server is answered over HTTP/3, 20 runs out of 20; the Notes record the runs.
- [ ] The first `Http3Only` request never runs past its timeout: a test with a stalled QUIC server ends within the timeout plus a small margin.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 2.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 3da6ca07 c2a0a654 3d836d5e.
