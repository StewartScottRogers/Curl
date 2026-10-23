---
id: BL-3163
title: Enforce -y/--speed-time with -Y/--speed-limit and abort with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/LowSpeedWatchdog.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitLibrary/TransferContextFactory.cs, Curl.Core.UnitTests/LowSpeedWatchdogTests.cs, Curl.Engine.UnitTests/LowSpeedEnforcementTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit1606SpeedCheck.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3163 — Enforce -y/--speed-time with -Y/--speed-limit and abort with exit 28

## Goal

`-y/--speed-time` with `-Y/--speed-limit` aborts a transfer that stays below the limit for the whole window, with exit 28 and curl's exact message.

## Context

Stewart reported on 2026-10-22: a server sends 10 bytes, waits 3 s, sends 10 more; `curl -sS -y 2 -Y 100000 URL`. Upstream exits 28 with `curl: (28) Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds`; Curl downloads everything and exits 0. `Curl.Core.UnitLibrary/LowSpeedWatchdog.cs` already exists, and `Curl.Engine.UnitLibrary/TransferPerformer.cs` (`StartLowSpeedWatchdog`) and `TransferContextFactory.cs` (`WatchedOutput`) wire it, so first find why it does not fire here (a watchdog fed only when bytes arrive can never see a stall; it needs a timer on the injected `TimeProvider`). The check must follow curl 8.22.0's sliding window (`Curl_speedcheck` in lib/speedcheck.c): the window restarts whenever throughput reaches the limit, and the abort happens when it has stayed below for `speed-time` seconds. It must apply to every transfer that goes through the transfer loop (HTTP, FTP and the others), not only HTTP; confirm which protocols feed the watchdog and, if one does not, feed it from the shared path rather than per protocol. Use `TimeProvider`, never `Thread.Sleep`. Measure the exact text and exit code with the target curl via `Record-CurlExchange.ps1` before pinning.

## Acceptance criteria

- [x] `Curl.Core.UnitTests/LowSpeedWatchdogTests.cs` (extend) has a test with a fake `TimeProvider` where 10 bytes arrive, 3 fake seconds pass and 10 more arrive, with limit 100000 and time 2: the watchdog aborts with `CurlExitCode.OperationTimedOut` (28) and message `Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds`.
- [x] A test where throughput stays above the limit for the whole transfer does not abort, and a test where throughput dips below the limit for less than the window and recovers does not abort.
- [x] `Curl.Engine.UnitTests/LowSpeedEnforcementTests.cs` (new) shows the abort end to end through the engine for an HTTP fake and for an FTP fake, each with exit 28 and the exact message.
- [x] `-y` without `-Y` (and `-Y` without `-y`) behaves as curl 8.22.0 does, pinned by a test with the measurement noted.
- [x] `dotnet build Curl.Core.UnitLibrary Curl.Engine.UnitLibrary -warnaserror` is clean; the fast tests of `Curl.Core.UnitTests` and `Curl.Engine.UnitTests` pass; no test sleeps or needs the network.

## Notes

- Cause: `LowSpeedWatchdog` already checks once a second on the injected `TimeProvider`, and the engine wires it for every scheme through the shared `TransferContextFactory` output wrapper (`WatchedOutput`), so HTTP, FTP and the rest all feed it. But it started the slow spell at its first check (second 1), so `-y 2` aborted at t=3, while curl's first `Curl_speedcheck` runs at the transfer's start with speed 0 and aborts at t=2. With Stewart's 3 s gap, Curl's t=3 check came after the data resumed. Fix: the slow spell starts when the watchdog is created. No change was needed in `TransferPerformer.cs` or `TransferContextFactory.cs`.
- Measured 2026-10-22 against loopback PowerShell servers. The platform curl here is 8.21.0 (Schannel), not 8.22.0; `lib/speedcheck.c` behaves the same in both. 10 bytes, 3 s pause, 10 bytes with `-sS -y 2 -Y 100000`: exit 28 after about 2.2 s with `curl: (28) Operation too slow. Less than 100000 bytes/sec transferred the last 2 seconds`. Curl exited 0 before the fix and exits 28 with the same line after it. Headers then a 4 s stall: `-y 2` alone exits 28 with `Less than 1 bytes/sec transferred the last 2 seconds` (curl and Curl); `-Y 100000` alone exits 0 (30 s default) in both. The existing `StartFromCommandLine_*` tests pin that, and `StartFromCommandLine_LimitWithoutTime` now trips at 30 s, not 31.
- Added `Curl.Conformance.UnitProgram.UnitLibrary/Limits/Unit1606SpeedCheck.cs` to touches: the upstream unit1606 port mapped the watchdog's first timer check to upstream's first check, so it needed shifting by one second (creating the watchdog is now upstream's `now=1` check). No task in Doing on origin/work/dark-factory named it.
- The engine test drives fake `http` and `ftp` handlers that write 10 bytes and stall, through `ManagedEasyTransfer` with the tool's progress set. The watchdog sits in the shared context path, so a fake handler per scheme shows each scheme is covered.
- No ADR: this is a bug fix to match measured curl, not a new design decision. The CI criterion is met by keeping the tests platform-neutral (fake clocks, no network, no paths). Measure-CodeQuality on Curl.Core.UnitLibrary and Curl.Conformance.UnitProgram.UnitLibrary: 0 failing members. Solution fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: eb8085c5 9619539a f29434cd d1579f1b df3c3df4.
- 2026-10-22: Doing -> Done. -y/-Y abort a transfer below the limit for the speed time from its start, exit 28 with curl's message, as measured
