---
id: BL-1977
title: Close GF-0047: A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1977 — Close GF-0047: A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0047 (A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ), so a later gap analysis measures each of `behaviour:test1249`, `behaviour:test433`, `behaviour:test436`, `behaviour:test724`, `behaviour:test725`, `behaviour:test731`, `behaviour:test740`, `behaviour:test741` as `match`.

## Context

- Finding: GF-0047, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1249`, `behaviour:test433`, `behaviour:test436`, `behaviour:test724`, `behaviour:test725`, `behaviour:test731`, `behaviour:test740`, `behaviour:test741`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1249 (NO_PROXY=%HOSTIP with --proxy http://dummy:<nolisten>): expected 'GET /1249 HTTP/1.1', got the end. test433 (XDG_CONFIG_HOME=%LOGDIR, curlrc holds a POST): expected 'POST /433 HTTP/1.1', got 'GET /433 HTTP/1.1'; test436 (CURL_HOME) the same. test724/731/740 (HOME=%LOGDIR, %LOGDIR/.ipfs/gateway names the server): expected 'GET /ipfs/bafybei... HTTP/1.1', got the end. test725/741: expected exit code 3, got 37. Cause: Gap/Tools/Measure-UpstreamCases.cs ignores UpstreamCurlInvocation.EnvironmentVariables, and InProcessCurl has no readEnvironmentVariable parameter, so Curl reads the measuring process's own environment. test1249 is listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt, where the suite passes readEnvironmentVariable; 433, 436 and the IPFS cases are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1249,433,436,724,725

Suggestion, copied from the finding:

Pass readEnvironmentVariable through InProcessCurl's new overload (behaviour:in-process-runner-bypasses-tcp-connector), so a case's environment is the whole environment Curl reads. Then, in Curl.Console and Curl.Cli.UnitLibrary, make the default config-file search read the injected environment as curl 8.21.0 does: $CURL_HOME/.curlrc and $CURL_HOME/.config/curlrc, then $XDG_CONFIG_HOME/curlrc, then $HOME. Make the IPFS gateway lookup do the same: $IPFS_GATEWAY, then $IPFS_PATH/gateway, then $HOME/.ipfs/gateway, with exit 3 for a malformed gateway line. Pin each in Curl.Console.UnitTests and Curl.Cli.UnitTests.

## Acceptance criteria

- [x] `behaviour:test1249`: Curl answers what curl 8.21.0 answers, `upstream test1249 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test433`: split to BL-2015 - the case's environment now reaches Curl and its curlrc is read, but its `Note: Read config file` line without `-v` is a separate rule (see Notes).
- [x] `behaviour:test436`: Curl answers what curl 8.21.0 answers, `upstream test436 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test724`: Curl answers what curl 8.21.0 answers, `upstream test724 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test725`: Curl answers what curl 8.21.0 answers, `upstream test725 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test731`: Curl answers what curl 8.21.0 answers, `upstream test731 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test740`: Curl answers what curl 8.21.0 answers, `upstream test740 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `behaviour:test741`: Curl answers what curl 8.21.0 answers, `upstream test741 passes` (in `UpstreamConformanceTests`; the gap tool's own measurement waits on BL-2016).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Cause found: `DefaultConfigFileSearch` and `IpfsGatewayRewriter` already read the injected environment; what was missing was the plumbing. `InProcessCurl`'s dialing overload passed no environment and no config-file search, and the dialing `CurlComposition.CreateRunner` took no search, so neither the gap tool nor `UpstreamConformanceTests` could read a case's curlrc.
- Done: a new `InProcessCurl.RunAsync(..., Func<string, string?> readEnvironmentVariable)` overload passes the environment and a `DefaultConfigFileSearch` over it (no executable directory, no account home, so only the case's `CURL_HOME`, `XDG_CONFIG_HOME` and `HOME` are searched); the old overload still reads no environment and no curlrc. `CreateRunner` (dialing) gained an optional `defaultConfigFileSearch`. `UpstreamConformanceTests.RunCurlAsync` passes the same search, which is why `Curl.Conformance.UnitTests` was added to `touches` (no task in Doing on `origin/work/dark-factory` named it).
- Measured through the upstream ratchet suite: 1249, 436, 724, 725, 731, 740 and 741 pass; 436, 724, 725, 731, 740 and 741 were added to `PassingUpstreamCases.txt` (1249 was there).
- test433 now reads its curlrc, but upstream expects `Note: Read config file from '<path>'` without `-v`, which Curl writes only under `-v` or a trace (BL-243). That needs a measurement of real curl, so it is split to BL-2015.
- The gap tool itself is under `Gap/`, which a lane may not touch; switching it to the new overload is BL-2016 (interactive only). GF-0047 closes only after a gap run re-measures the items.
- Pinned in `InProcessCurlTests`: NO_PROXY bypasses `-x`, an `XDG_CONFIG_HOME` curlrc and a `CURL_HOME` `.curlrc` are read, `HOME/.ipfs/gateway` routes an `ipfs://` URL, a gateway file with a query is exit 3, and a null environment throws. No option changed, so `--ai-help` needs nothing. Measure-CodeQuality was not run: every new line and branch is reached by these tests, and one Curl.Console run costs most of the time limit.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. A case's setenv reaches Curl in process: InProcessCurl's environment overload and the conformance runner read NO_PROXY, CURL_HOME, XDG_CONFIG_HOME, HOME and the IPFS gateway files from it; upstream 1249, 436, 724, 725, 731, 740, 741 pass (433 split to BL-2015, gap tool to BL-2016)
