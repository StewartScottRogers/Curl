---
id: GF-0047
title: A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ
area: behaviour
platform: windows
key: behaviour:in-process-runner-ignores-case-environment
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-10_0657
closed: 2026-10-11_0936
regression: false
items: [behaviour:test1249, behaviour:test433, behaviour:test436, behaviour:test724, behaviour:test725, behaviour:test731, behaviour:test740, behaviour:test741]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1977
tasks: [BL-1977]
---
# GF-0047 - A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ

## Summary

Curl differs from upstream curl in behaviour: A case's <setenv> (NO_PROXY, HOME, XDG_CONFIG_HOME, CURL_HOME) never reaches Curl in process, so curlrc, .ipfs/gateway and NO_PROXY lookups differ.

## Evidence

Every item expects 'upstream test<N> passes'. test1249 (NO_PROXY=%HOSTIP with --proxy http://dummy:<nolisten>): expected 'GET /1249 HTTP/1.1', got the end. test433 (XDG_CONFIG_HOME=%LOGDIR, curlrc holds a POST): expected 'POST /433 HTTP/1.1', got 'GET /433 HTTP/1.1'; test436 (CURL_HOME) the same. test724/731/740 (HOME=%LOGDIR, %LOGDIR/.ipfs/gateway names the server): expected 'GET /ipfs/bafybei... HTTP/1.1', got the end. test725/741: expected exit code 3, got 37. Cause: Gap/Tools/Measure-UpstreamCases.cs ignores UpstreamCurlInvocation.EnvironmentVariables, and InProcessCurl has no readEnvironmentVariable parameter, so Curl reads the measuring process's own environment. test1249 is listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt, where the suite passes readEnvironmentVariable; 433, 436 and the IPFS cases are not. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1249,433,436,724,725

## Suggestion

Pass readEnvironmentVariable through InProcessCurl's new overload (behaviour:in-process-runner-bypasses-tcp-connector), so a case's environment is the whole environment Curl reads. Then, in Curl.Console and Curl.Cli.UnitLibrary, make the default config-file search read the injected environment as curl 8.21.0 does: $CURL_HOME/.curlrc and $CURL_HOME/.config/curlrc, then $XDG_CONFIG_HOME/curlrc, then $HOME. Make the IPFS gateway lookup do the same: $IPFS_GATEWAY, then $IPFS_PATH/gateway, then $HOME/.ipfs/gateway, with exit 3 for a malformed gateway line. Pin each in Curl.Console.UnitTests and Curl.Cli.UnitTests.

## Measurements

- 2026-10-10_0657: 8 of 8 items are gaps.
- 2026-10-11_0936: 0 of 8 items are gaps.
- 2026-10-11_1950: 0 of 8 items are gaps.
- 2026-10-12_0951: 0 of 8 items are gaps.
- 2026-10-12_2042: 0 of 8 items are gaps.
- 2026-10-13_1406: 0 of 8 items are gaps.
- 2026-10-14_0904: 0 of 8 items are gaps.
- 2026-10-15_0304: 0 of 8 items are gaps.
- 2026-10-15_0543: 0 of 8 items are gaps.
- 2026-10-15_0751: 0 of 8 items are gaps.
- 2026-10-15_1657: 0 of 8 items are gaps.
- 2026-10-17_2225: 0 of 8 items are gaps.
- 2026-10-19_0804: 0 of 8 items are gaps.
- 2026-10-19_1329: 0 of 8 items are gaps.
- 2026-10-22_0148: 0 of 8 items are gaps.
- 2026-10-23_1637: 0 of 8 items are gaps.
- 2026-10-25_1542: 0 of 8 items are gaps.
- 2026-10-30_2103: 0 of 8 items are gaps.
- 2026-10-31_1828: 0 of 8 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
- 2026-10-10_0756: Filed BL-1977.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
