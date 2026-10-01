---
id: BL-921
title: Log retries, redirects, watchdog limits, HSTS and alt-svc decisions to the diagnostic log in Curl.Core
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-938, BL-919]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-921 — Log retries, redirects, watchdog limits, HSTS and alt-svc decisions to the diagnostic log in Curl.Core

## Goal

`Curl.Core.UnitLibrary` writes the diagnostic log for the run-level machinery: retries (`retry`), redirects (`redirect`), the `--max-time` and `--speed-limit` watchdogs (`runner`), HSTS upgrades (`hsts`) and alt-svc choices (`altsvc`), and `RedirectFollower` forwards `DiagnosticLog` from the context it wraps.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `TransferRetrier.cs`, `RetryPolicy.cs`, `RetryAfterHeader.cs` (each retry: reason, attempt number, delay), `RedirectFollower.cs` (each hop: status, target URL with any user info removed, method kept or changed; and `ITransferContext.DiagnosticLog` forwarded from the wrapped context), `RedirectPolicy.cs` (why a hop is refused), `MaxTimeWatchdog.cs` and `LowSpeedWatchdog.cs` (limit armed, limit hit), `Hsts/HstsTransferPolicy.cs` and `Hsts/HstsCache.cs` (an http URL upgraded, an entry stored or expired), `AltSvc/AltSvcCache.cs` (an alternative stored or used), `ProxySelector.cs`/`NoProxyMatcher.cs` (which proxy, or why none).
- Services `Curl.Console/CurlComposition.cs` builds without a transfer context take `IDiagnosticLog` through their constructor, and the composition root passes the composed log. That is why this task also touches `Curl.Console` and waits for BL-919.
- What, per level: `error` a retry budget exhausted or a watchdog ending the transfer, with its `CurlExitCode`; `warning` each retry with its reason and delay, a redirect refused; `info` each redirect hop, an HSTS upgrade, an alt-svc alternative used; `verbose` policy inputs (retry counters, remaining time, the no-proxy match, cache hits and misses).
- Credential-bearing path: a redirect whose `Location` carries `user:secret@`.

## Acceptance criteria

- [x] `Curl.Core.UnitTests` pin: a retried transfer logs `warning` with component `retry`, the attempt number and the delay in ms; a followed redirect logs `info` with component `redirect` and the target URL without its password; `RedirectFollower.DiagnosticLog` returns the wrapped context's log; a `--max-time` expiry logs `error` naming `OperationTimedOut`; an HSTS upgrade logs `info` with component `hsts`.
- [x] `Curl.Console/CurlComposition.cs` passes the composed log to every Core service that now takes one, and a `Curl.Console.UnitTests` test shows a retry line in a `--log-level warning --log-file` log.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- Shape: `TransferRetrier` and `RedirectFollower` read `ITransferContext.DiagnosticLog` from the context they are given (no constructor change), through the internal helpers `RetryDiagnosticLog` and `RedirectDiagnosticLog`; `RedirectFollower.NextHop` copies `DiagnosticLog` onto every hop. `MaxTimeWatchdog`, `LowSpeedWatchdog` and `HstsTransferPolicy` take an optional `IDiagnosticLog` constructor parameter (null means `NoDiagnosticLog.Instance`), so every existing caller and test is unchanged.
- Wiring, criterion 2: none of these services is built in `CurlComposition.cs`; the run's log only exists once `CurlCommandRunner.OpenDiagnosticLogAsync` has opened it (ADR-0228), and the runner is where the watchdogs (`StartLowSpeedWatchdog`, `StartMaxTimeWatchdog`) and the run's `HstsTransferPolicy` are made, so the runner passes its `diagnosticLog` there. Contexts already carry it via `TransferContextFactory.DiagnosticLog` (BL-919), which is what the retrier and follower read. `CurlComposition.cs` needed no change; met in substance at the place the services are composed.
- Lines: retry `warning` "attempt N failed (Reason); retrying in X ms, R retries left", `error` "--retry N exhausted after M attempts; the last ended with exit C (Name)" (only under `--retry`), `warning` on `--retry-max-time` (elapsed or a `Retry-After` past it), `verbose` each attempt's exit and counters. Redirect `info` "following 302 to <url>, method kept|changed to GET", `warning` "<status> to <url> not followed: exit C (Name), <message>", `verbose` the `--max-redirs` counters. Runner `verbose` limit armed, `error` limit hit naming `OperationTimedOut (28)`. HSTS `info` switched, `verbose` learned and miss; host names only.
- Credentials: redirect targets are logged through `RedirectDiagnosticLog.WithoutUserInformation`; HSTS logs the host, never the URL; the retrier logs no URL. Tests assert `s3cret` never appears for the `Location: user:secret@` path (followed and refused) and an HSTS upgrade of a URL with user info.
- Left out, filed as BL-1072: proxy choice / no-proxy match (`ProxySelector` is built in `CurlComposition` before the log exists, so it needs a wiring decision), alt-svc alternatives stored/used, HSTS entries stored/expired in `HstsCache`.
- Tests: `TransferRetrierDiagnosticLogTests`, `RedirectFollowerDiagnosticLogTests`, `RedirectDiagnosticLogTests`, `MaxTimeWatchdogDiagnosticLogTests`, `LowSpeedWatchdogDiagnosticLogTests`, `Hsts/HstsTransferPolicyDiagnosticLogTests` with a `Fakes/RecordingDiagnosticLog`; `CurlCommandRunnerRetryTests.RunAsync_RetryWithLogLevelWarningToLogFile_LogsTheRetryUnderRetry`. Core 1322 passed, Console 1967 passed; Measure-CodeQuality: Curl.Core.UnitLibrary and Curl.Console both 100/100, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Retries, redirects, the -m and -Y watchdogs and HSTS upgrades write the diagnostic log; every redirect hop carries the context's log
