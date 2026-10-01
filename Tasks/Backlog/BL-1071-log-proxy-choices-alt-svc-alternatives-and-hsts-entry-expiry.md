---
id: BL-1071
title: Log proxy choices, alt-svc alternatives and HSTS entry expiry to the diagnostic log
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-921]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1071 — Log proxy choices, alt-svc alternatives and HSTS entry expiry to the diagnostic log

## Goal

Curl's own diagnostic log (`--log-level`) records which proxy a transfer uses or why none (component `proxy`), each `--alt-svc` alternative stored or used (component `altsvc`), and each HSTS entry stored or expired (component `hsts`), finishing what BL-921 left out of its Context list.

## Context

- Rules: ADR-0222 (levels, components, never-logged values, "test `IsEnabled` first"); ADR-0228 (the runner opens the log once the command line is accepted).
- BL-921 logged retries, redirects, the watchdogs and HSTS upgrades. It left these out: `ProxySelector.cs`/`NoProxyMatcher.cs` (which proxy, or why none; `verbose` the no-proxy match), `AltSvc/AltSvcCache.cs` and `Curl.Console/AltSvcTransferCache.cs` (an alternative stored or used: `info` used, `verbose` cache hits and misses), `Hsts/HstsCache.cs` (an entry stored or expired: `verbose`).
- Wiring snag: `CurlComposition.CreateTransferDispatch` builds the `ProxySelector` before the run's log exists (it is opened per run by `CurlCommandRunner.OpenDiagnosticLogAsync`). Either pass the log per call (e.g. through `TransferProxySelection.TrySelect`, which has the transfer's context nearby) or give the selector a log the runner sets once it opens; decide and record an ADR.
- A proxy URL may carry `user:password@`: log the proxy's scheme, host and port only.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests` pin: a chosen proxy logs `info` with component `proxy` naming scheme, host and port and no password; a `--noproxy` match logs `verbose` naming the matching entry; an alt-svc alternative used logs `info` with component `altsvc`; an HSTS entry stored and one expired log `verbose` with component `hsts`.
- [ ] A `Curl.Console.UnitTests` test shows a `[proxy]` line in a `--log-level info --log-file` log for a `-x http://user:secret@proxy:3128` transfer, and that the log does not contain `secret`.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

## Log

- 2026-10-01: Created.
