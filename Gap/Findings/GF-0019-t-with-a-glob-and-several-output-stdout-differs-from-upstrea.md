---
id: GF-0019
title: -T with a glob and several --output: stdout differs from upstream's %EMPTY expectation
area: behaviour
key: behaviour:upload-glob-stdout
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test2013, behaviour:test2014]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1812
tasks: [BL-1812]
---
# GF-0019 - -T with a glob and several --output: stdout differs from upstream's %EMPTY expectation

## Summary

Curl differs from upstream curl in behaviour: -T with a glob and several --output: stdout differs from upstream's %EMPTY expectation.

## Evidence

Both items expect 'upstream test<N> passes'. test2013 actual: <verify><stdout> differs at byte 0 (line 1): expected '%EMPTY', got the end. test2014 has the same shape. Curl wrote nothing to stdout. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 2013,2014

## Suggestion

First confirm against the reference curl what test2013/2014 write to stdout with -T '{1,2}' and --output per transfer (Curl.Console's output routing). If the reference also writes nothing, the difference is the harness not reading upstream's %EMPTY. If the reference writes something, route the upload responses that have no --output left to stdout as curl does.

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.
- 2026-10-08_2029: 2 of 2 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1812.
