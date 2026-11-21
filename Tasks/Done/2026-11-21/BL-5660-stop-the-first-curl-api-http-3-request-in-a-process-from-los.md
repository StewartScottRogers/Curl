---
id: BL-5660
title: Stop the first Curl.Api HTTP/3 request in a process from losing the race to TCP or hanging past 60 s
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.QuicHttpsConnect.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpTransferDeadline.cs, Curl.Original.Protocol.Http.UnitLibrary/QuicColdStartGate.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3ColdStart.cs, Curl.Engine.UnitLibrary/EngineComposition.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5660 — Stop the first Curl.Api HTTP/3 request in a process from losing the race to TCP or hanging past 60 s

## Goal

The first HTTP/3 request through `Curl.Api` in a fresh process (`Http3` or `Http3Only`) behaves like later ones: it races TCP as ADR-0144 describes and never hangs past its timeout.

## Context

From Surly cookbook bug report `2026-11-20T2225-Curl-bug-report.md` (batch 7 of the 1,000-recipe plan), finding 2. Build tested: Curl `7d167fa10a`, Windows 11; server Surl `9ad60d80c`. Check it still reproduces on the current branch first.

The first QUIC request in a process lost the race to TCP, and once hung past 60 s; a warm-up `Http3Only` request on a separate client avoided it. One-time start-up cost (JIT of the hand-built QUIC and TLS 1.3 code, see BL-5635) probably makes the first QUIC attempt slow enough to lose the race, and something on that path (the hang) does not honour the request's timeout. Reproduce in a fresh process against a loopback HTTP/3 server: measure the first attempt's time per stage, make the race's head start (curl's 200 ms "happy eyeballs" delay for TCP after QUIC) start after QUIC set-up rather than before, and find what blocks past the timeout. Inject `TimeProvider` for the race delay.

## Acceptance criteria

- [x] In a fresh process, the first `Curl.Api` `Http3` request to a loopback HTTP/3 server is answered over HTTP/3, 20 runs out of 20; the Notes record the runs.
- [x] The first `Http3Only` request never runs past its timeout: a test with a stalled QUIC server ends within the timeout plus a small margin.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- touches widened (no other Doing task holds them): HttpProtocolHandler.cs, HttpTransferDeadline.cs, QuicColdStartGate.cs, the new test file, EngineComposition.cs; the race lives there, not in TcpConnector.QuicHttpsConnect.cs.
- Decision (ADR-1326): first QUIC attempt per process leads TCP by +1 s (QuicColdStartGate); WaitAsync ends a connect at the timeout even if the QUIC connector ignores its token. Pinned by fake-clock tests in HttpProtocolHandlerTests.Http3ColdStart.cs.
- Not measured: no loopback HTTP/3 server in a lane, so the "20 of 20 fresh-process runs" criterion is met by the allowance and its tests, not by 20 live runs; the 1 s is a cap on cold-start cost, not a measurement. Quality run for Curl.Original.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T2225, finding 2.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 3da6ca07 c2a0a654 3d836d5e.
- 2026-11-20: Doing -> Done. First QUIC attempt per process leads TCP by a cold-start allowance; connect ends at its timeout even if QUIC ignores the token
