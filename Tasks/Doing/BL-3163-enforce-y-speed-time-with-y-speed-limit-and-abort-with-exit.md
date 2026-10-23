---
id: BL-3163
title: Enforce -y/--speed-time with -Y/--speed-limit and abort with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/LowSpeedWatchdog.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Core.UnitTests/LowSpeedWatchdogTests.cs, Curl.Engine.UnitTests/LowSpeedEnforcementTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3163 — Enforce -y/--speed-time with -Y/--speed-limit and abort with exit 28

## Goal

`-y/--speed-time` with `-Y/--speed-limit` aborts a transfer that stays below the limit for the whole window, with exit 28 and curl's exact message.

## Context

Stewart reported on 2026-10-22: a server sends 10 bytes, waits 3 s, sends 10 more; `curl -sS -y 2 -Y 100000 URL`. Upstream exits 28 with `curl: (28) Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds`; Curl downloads everything and exits 0. `Curl.Core.UnitLibrary/LowSpeedWatchdog.cs` already exists, and `Curl.Engine.UnitLibrary/TransferPerformer.cs` (`StartLowSpeedWatchdog`) and `TransferContextFactory.cs` (`WatchedOutput`) wire it, so first find why it does not fire here (a watchdog fed only when bytes arrive can never see a stall; it needs a timer on the injected `TimeProvider`). The check must follow curl 8.22.0's sliding window (`Curl_speedcheck` in lib/speedcheck.c): the window restarts whenever throughput reaches the limit, and the abort happens when it has stayed below for `speed-time` seconds. It must apply to every transfer that goes through the transfer loop (HTTP, FTP and the others), not only HTTP; confirm which protocols feed the watchdog and, if one does not, feed it from the shared path rather than per protocol. Use `TimeProvider`, never `Thread.Sleep`. Measure the exact text and exit code with the target curl via `Record-CurlExchange.ps1` before pinning.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests/LowSpeedWatchdogTests.cs` (extend) has a test with a fake `TimeProvider` where 10 bytes arrive, 3 fake seconds pass and 10 more arrive, with limit 100000 and time 2: the watchdog aborts with `CurlExitCode.OperationTimedOut` (28) and message `Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds`.
- [ ] A test where throughput stays above the limit for the whole transfer does not abort, and a test where throughput dips below the limit for less than the window and recovers does not abort.
- [ ] `Curl.Engine.UnitTests/LowSpeedEnforcementTests.cs` (new) shows the abort end to end through the engine for an HTTP fake and for an FTP fake, each with exit 28 and the exact message.
- [ ] `-y` without `-Y` (and `-Y` without `-y`) behaves as curl 8.22.0 does, pinned by a test with the measurement noted.
- [ ] `dotnet build Curl.Core.UnitLibrary Curl.Engine.UnitLibrary -warnaserror` is clean; the fast tests of `Curl.Core.UnitTests` and `Curl.Engine.UnitTests` pass; no test sleeps or needs the network.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: eb8085c5 9619539a f29434cd d1579f1b df3c3df4.
