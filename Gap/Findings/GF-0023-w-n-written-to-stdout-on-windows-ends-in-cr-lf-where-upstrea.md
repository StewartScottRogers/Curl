---
id: GF-0023
title: -w '\n' written to stdout on Windows ends in CR LF where upstream expects LF
area: behaviour
key: behaviour:writeout-stdout-line-feed
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1341]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0023 - -w '\n' written to stdout on Windows ends in CR LF where upstream expects LF

## Summary

Curl differs from upstream curl in behaviour: -w '\n' written to stdout on Windows ends in CR LF where upstream expects LF.

## Evidence

behaviour:test1341 (-J -O -D - -w 'curl saved to filename %{filename_effective}\n') expected 'upstream test1341 passes', actual: <verify><file2> stdout1341 differs at byte 342 (line 9): expected '... name1341\n', got '... name1341\r\n'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1341

## Suggestion

In Curl.Output.UnitLibrary / Curl.Console's -w writer, write a -w line feed to stdout as LF, as upstream's <file2> expects. Keep CR LF only for the %output{} file targets ADR-0081 covers, unless a fresh measurement of the Windows reference curl shows CR LF on stdout too. In that case record it in the item's notes for a 'reference-diverges' cross-check.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
