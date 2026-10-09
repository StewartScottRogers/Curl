---
id: GF-0016
title: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs
area: behaviour
key: behaviour:remote-header-name-with-redirect-and-retry
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1642, behaviour:test1643, behaviour:test3036]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-1809
tasks: [BL-1809]
---
# GF-0016 - -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs

## Summary

Curl differs from upstream curl in behaviour: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs.

## Evidence

Every item expects 'upstream test<N> passes'. test1642 (-J -L -O, no Content-Disposition): <verify><file> <LOGDIR>/16420002 differs at byte 0: expected '12345', got the end (no such file). test1643 (two redirects) has the same shape. test3036 (--no-clobber --output-dir ... -OJ --retry 1 --retry-all-errors): stderr differs from the reference curl, which exits 23. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1642,1643,3036

## Suggestion

In Curl.Console's -O/-J output naming: with -J and -L and no Content-Disposition, name the file after the last URL followed (the final Location's last path segment), as curl 8.21.0 does. With --no-clobber and --retry, fail on an existing file with exit 23 and the reference's message, as upstream test3036 expects.

## Measurements

- 2026-10-08_1640: 3 of 3 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1809.
