---
id: BL-921
title: Log retries, redirects, watchdog limits, HSTS and alt-svc decisions to the diagnostic log in Curl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-916, BL-919]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-921 — Log retries, redirects, watchdog limits, HSTS and alt-svc decisions to the diagnostic log in Curl.Core

## Goal

`Curl.Core.UnitLibrary` writes the diagnostic log for the run-level machinery: retries (`retry`), redirects (`redirect`), the `--max-time` and `--speed-limit` watchdogs (`runner`), HSTS upgrades (`hsts`) and alt-svc choices (`altsvc`), and `RedirectFollower` forwards `DiagnosticLog` from the context it wraps.

## Context

- The rules are BL-915's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-916's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `TransferRetrier.cs`, `RetryPolicy.cs`, `RetryAfterHeader.cs` (each retry: reason, attempt number, delay), `RedirectFollower.cs` (each hop: status, target URL with any user info removed, method kept or changed; and `ITransferContext.DiagnosticLog` forwarded from the wrapped context), `RedirectPolicy.cs` (why a hop is refused), `MaxTimeWatchdog.cs` and `LowSpeedWatchdog.cs` (limit armed, limit hit), `Hsts/HstsTransferPolicy.cs` and `Hsts/HstsCache.cs` (an http URL upgraded, an entry stored or expired), `AltSvc/AltSvcCache.cs` (an alternative stored or used), `ProxySelector.cs`/`NoProxyMatcher.cs` (which proxy, or why none).
- Services `Curl.Console/CurlComposition.cs` builds without a transfer context take `IDiagnosticLog` through their constructor, and the composition root passes the composed log. That is why this task also touches `Curl.Console` and waits for BL-919.
- What, per level: `error` a retry budget exhausted or a watchdog ending the transfer, with its `CurlExitCode`; `warning` each retry with its reason and delay, a redirect refused; `info` each redirect hop, an HSTS upgrade, an alt-svc alternative used; `verbose` policy inputs (retry counters, remaining time, the no-proxy match, cache hits and misses).
- Credential-bearing path: a redirect whose `Location` carries `user:secret@`.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests` pin: a retried transfer logs `warning` with component `retry`, the attempt number and the delay in ms; a followed redirect logs `info` with component `redirect` and the target URL without its password; `RedirectFollower.DiagnosticLog` returns the wrapped context's log; a `--max-time` expiry logs `error` naming `OperationTimedOut`; an HSTS upgrade logs `info` with component `hsts`.
- [ ] `Curl.Console/CurlComposition.cs` passes the composed log to every Core service that now takes one, and a `Curl.Console.UnitTests` test shows a retry line in a `--log-level warning --log-file` log.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
