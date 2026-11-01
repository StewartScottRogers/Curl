---
id: GF-0044
title: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL
area: behaviour
platform: windows
key: behaviour:harness-pwd-with-blank-splits-command
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_2029
closed: 2026-10-10_0657
regression: false
items: [behaviour:test3009]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-1854
tasks: [BL-1854]
---
# GF-0044 - %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL

## Summary

Curl differs from upstream curl in behaviour: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL.

## Evidence

Measured: test3009 expected 'upstream test3009 passes', actual '<verify><protocol> differs at byte 94 (line 6): expected the end, got "GET /AppData/Local/Curl/gap/upstream/8.21.0/tests/not-there HTTP/1.1\r\n"'. The command is -O --output-dir %PWD/not-there, which upstream expects to fail with exit 23 after one request. %PWD is the release's tests folder under C:/Users/Stewart Rogers/AppData/Local, so 'Rogers/AppData/...' became a second URL. A copy of the case under a tests folder with no blank (Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/tests/data), rerun on 6383c570, passed. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 3009

## Suggestion

Curl's behaviour needs no change: it passes test3009 once %PWD holds no blank. Make Curl.Conformance.UnitLibrary's UpstreamCaseRunner own %PWD: take it as a parameter and refuse one that holds a blank, as it already does for %LOGDIR. Pin the refusal in Curl.Conformance.UnitTests. The office's Measure-UpstreamCases.cs must then pass a tests folder with no blank, for example by copying the release under the run folder. That part is office work, filed interactively.

## Measurements

- 2026-10-08_2029: 1 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-08_2029: Opened by gap-behaviour.
- 2026-10-08_2131: Filed BL-1854.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
