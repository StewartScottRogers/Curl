---
id: BL-245
title: Carry --request-target and --path-as-is into HttpRequestOptions in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-186, BL-191, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-245 — Carry --request-target and --path-as-is into HttpRequestOptions in Curl.Console

## Goal

`--request-target` and `--path-as-is` reach the HTTP handler from the command line.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W16. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Not in the plan's W list: BL-191 parses these and BL-186 honours them, but BL-236 covers only the transfer-encoding options.

## Acceptance criteria

- [x] `curl --path-as-is http://h/a/../b` and `curl -X OPTIONS --request-target '*' http://h/` send BL-186's measured request lines over a fake connector.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W16 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Plan (direct, one line of production code): `--path-as-is` already reached the handler, through `TransferContextFactory` setting `TransferContext.PathAsIs`, which parses the URL keeping its dot segments; only `--request-target` was missing. `HttpRequestOptionsMapping.FromCommandLine` now copies `CommandLineOptions.RequestTarget` into `HttpRequestOptions.RequestTarget`. No design decision was needed, so no ADR.
- Tests: `CurlCommandRunnerRequestTargetTests` runs the runner over `HttpProtocolHandler` and a `ScriptedConnector` and pins BL-186's four measured request lines: `-X OPTIONS --request-target '*'` gives `OPTIONS * HTTP/1.1`, `--request-target /x/../y?z` is sent verbatim, `--path-as-is /a/../b` keeps the dot segments, and the same URL without it becomes `/b`. The two `--request-target` tests failed before the mapping change.
- Verified: `dotnet build -warnaserror` clean; fast tests green across the solution (Curl.Console.UnitTests 813 passed); `dotnet format --verify-no-changes` clean. `Measure-CodeQuality.ps1 -Library Curl.Console -IncludeIntegration` reports 100% line, 100% branch, 0 failing members and worst CRAP 10. Without `-IncludeIntegration` the only failing member is `DiskWriteOutFileOpener.TryOpen`, which only Integration tests cover by design (BL-280). This task did not change it.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. --request-target reaches the HTTP request line from the command line, and --path-as-is is pinned end to end, matching curl 8.21.0
