---
id: GF-0022
title: -D % (headers to stderr) writes nothing
area: behaviour
key: behaviour:dump-header-to-stderr
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1489]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
task: BL-1815
tasks: [BL-1815]
---
# GF-0022 - -D % (headers to stderr) writes nothing

## Summary

Curl differs from upstream curl in behaviour: -D % (headers to stderr) writes nothing.

## Evidence

behaviour:test1489 (-D % -s) expected 'upstream test1489 passes', actual: <verify><stderr> differs at byte 0 (line 1): expected 'HTTP/1.1 200 OK', got the end. The reference curl exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1489

## Suggestion

In Curl.Cli.UnitLibrary and Curl.Console's -D target opening, accept '%' as stderr (curl 8.21.0's -D %) and write the received headers there, even under -s. Keep --ai-help's dump-header entry right.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1815.
