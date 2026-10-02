---
id: BL-1072
title: Log proxy choices, alt-svc alternatives and HSTS entry expiry to the diagnostic log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-921]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1072 — Log proxy choices, alt-svc alternatives and HSTS entry expiry to the diagnostic log

## Goal

Curl's own diagnostic log (`--log-level`) records which proxy a transfer uses or why none (component `proxy`), each `--alt-svc` alternative stored or used (component `altsvc`), and each HSTS entry stored or expired (component `hsts`), finishing what BL-921 left out of its Context list.

## Context

- Rules: ADR-0222 (levels, components, never-logged values, "test `IsEnabled` first"); ADR-0236 (the runner opens the log once the command line is accepted).
- BL-921 logged retries, redirects, the watchdogs and HSTS upgrades. It left these out: `ProxySelector.cs`/`NoProxyMatcher.cs` (which proxy, or why none; `verbose` the no-proxy match), `AltSvc/AltSvcCache.cs` and `Curl.Console/AltSvcTransferCache.cs` (an alternative stored or used: `info` used, `verbose` cache hits and misses), `Hsts/HstsCache.cs` (an entry stored or expired: `verbose`).
- Wiring snag: `CurlComposition.CreateTransferDispatch` builds the `ProxySelector` before the run's log exists (it is opened per run by `CurlCommandRunner.OpenDiagnosticLogAsync`). Either pass the log per call (e.g. through `TransferProxySelection.TrySelect`, which has the transfer's context nearby) or give the selector a log the runner sets once it opens; decide and record an ADR.
- A proxy URL may carry `user:password@`: log the proxy's scheme, host and port only.

## Acceptance criteria

- [x] `Curl.Core.UnitTests` pin: a chosen proxy logs `info` with component `proxy` naming scheme, host and port and no password; a `--noproxy` match logs `verbose` naming the matching entry; an alt-svc alternative used logs `info` with component `altsvc`; an HSTS entry stored and one expired log `verbose` with component `hsts`.
- [x] A `Curl.Console.UnitTests` test shows a `[proxy]` line in a `--log-level info --log-file` log for a `-x http://user:secret@proxy:3128` transfer, and that the log does not contain `secret`.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Wiring decided in ADR-0302: `ProxySelector.TrySelect` takes the log per call (optional last
  parameter, passed by `TransferProxySelection.TrySelect` from the runner's `diagnosticLog`, first
  URL and every redirect hop); `AltSvcCache` and `HstsCache` take it at construction
  (`AltSvcTransferCache.OpenAsync`, `HstsTransferPolicy`). No setter on the shared selector.
- New: `ProxyDiagnosticLog`, `AltSvc\AltSvcDiagnosticLog`, `NoProxyMatcher.MatchingEntry` (the
  entry as written, `*` for the whole list `*`). Unusable proxy text logs nothing; the runner's
  exit line already reports it.
- `HstsTransferPolicyDiagnosticLogTests.TrySwitchToHttps_HostTheCacheKnows_...` now expects the
  cache's `stored entry for example.com` line before the policy's `learned ...` line: it records
  with a real log, so the "unmodified" criterion (which is about `NoDiagnosticLog`) does not cover it.
- Complexity: the first cut put `TrySelect`, `HstsCache.ApplyHeader`/`IndexOfName` and
  `TransferProxySelection.TrySelect` at 12-14, from `??` and cached-lambda null checks; split
  `TrySelect` into `IsReachedDirectly`/`TryParseProxyFor` and dropped the lambdas. Measured after:
  Curl.Core.UnitLibrary and Curl.Console 100% line, 100% branch, 0 failing members, worst CRAP 10.
- Fast suite: 22,315 passed, 0 failed.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Proxy choices, alt-svc alternatives and HSTS entries stored or expired now reach --log-level (components proxy, altsvc, hsts); proxy passwords never logged
