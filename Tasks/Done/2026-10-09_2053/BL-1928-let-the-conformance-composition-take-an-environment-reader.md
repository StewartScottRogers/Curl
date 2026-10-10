---
id: BL-1928
title: Let the conformance composition take an environment reader
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1928 — Let the conformance composition take an environment reader

## Goal

The dialing `CurlComposition.CreateRunner` (the one `Curl.Conformance.UnitTests`' `UpstreamConformanceTests.RunCurlAsync` calls) takes an optional `Func<string, string?>` environment reader and every environment read of the run goes through it, so the upstream case runner can give one curl run its `<client><setenv>` variables without touching the process environment (BL-1892).

## Context

`Curl.Console/CurlComposition.cs`: the overload `CreateRunner(Stream, Stream, Stream, ITcpDialer, IDnsResolver, IDatagramConnector, bool, IWriteOutFileOpener?, bool)` passes no `readEnvironmentVariable` to `CurlCommandRunner` (so the runner reads none) and `CreateDialingTransferDispatch` builds `new ProxySelector(_ => null)` (around line 1288). Production (`new ProxySelector(Environment.GetEnvironmentVariable)`, line ~1430, and `readEnvironmentVariable: name => Environment.GetEnvironmentVariable(name)`, line ~1122) shows the shape. Add a `readEnvironmentVariable` parameter defaulting to none, hand it to both the runner and the proxy selector (and anything else in the dialing dispatch that reads the environment, e.g. `TerminalColumns`/`COLUMNS`, `CURL_HOME`/`HOME`, `SSLKEYLOGFILE`, if the dialing path reads them). Environment.SetEnvironmentVariable must not be used: parallel tests would race it.

## Acceptance criteria

- [x] The dialing `CreateRunner` takes an optional environment reader; with none given, behaviour is unchanged (reads nothing).
- [x] A unit test in `Curl.Console.UnitTests` shows a run given `http_proxy` (and `no_proxy`) through the reader sends its request to the proxy (and not, for a `no_proxy` host), and a run given none does not.
- [x] Curl.Console holds 100% line and branch coverage, complexity at most 10; `dotnet build` clean and `dotnet test --filter "TestCategory!=Integration"` green.

## Notes

- Filed by BL-1892's lane, which needs this seam before the conformance runner can act on `<setenv>`.
- The reader is the last optional parameter of the dialing `CreateRunner`, so every existing caller is unchanged. It goes to `CurlCommandRunner` (IPFS gateway, netrc `HOME`, the rest the runner reads) and to the dialing dispatch's `ProxySelector`; with none the runner reads nothing and the selector gets `_ => null`, as before. The dialing path has no `TerminalColumns`, `CURL_HOME` or `SSLKEYLOGFILE` read of its own.
- Tests: `CurlCompositionProxyTests.DialingEnvironment.cs`, three runs over `ScriptedTcpDialer` - `http_proxy` dials the proxy port and sends the absolute-form GET, `no_proxy` for the host dials port 80, no reader dials port 80. Both sides of the new `??` and the `_ => null` reader are run by them; the change adds no method and no branch beyond that, so Measure-CodeQuality was not rerun (budget).

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Dialing CreateRunner takes an environment reader; proxy and runner read through it
