---
id: BL-1977
title: Close GF-0047: A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-10
completed:
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

- [ ] `behaviour:test1249`: Curl answers what curl 8.21.0 answers, `upstream test1249 passes`, so the item measures `match`.
- [ ] `behaviour:test433`: Curl answers what curl 8.21.0 answers, `upstream test433 passes`, so the item measures `match`.
- [ ] `behaviour:test436`: Curl answers what curl 8.21.0 answers, `upstream test436 passes`, so the item measures `match`.
- [ ] `behaviour:test724`: Curl answers what curl 8.21.0 answers, `upstream test724 passes`, so the item measures `match`.
- [ ] `behaviour:test725`: Curl answers what curl 8.21.0 answers, `upstream test725 passes`, so the item measures `match`.
- [ ] `behaviour:test731`: Curl answers what curl 8.21.0 answers, `upstream test731 passes`, so the item measures `match`.
- [ ] `behaviour:test740`: Curl answers what curl 8.21.0 answers, `upstream test740 passes`, so the item measures `match`.
- [ ] `behaviour:test741`: Curl answers what curl 8.21.0 answers, `upstream test741 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
