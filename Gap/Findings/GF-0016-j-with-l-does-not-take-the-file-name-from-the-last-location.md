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
task: BL-2009
tasks: [BL-1809, BL-2009]
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
- 2026-10-08_2029: 1 of 3 items are gaps.
- 2026-10-10_0657: 1 of 3 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1809.
- 2026-10-10_0756: Filed BL-2009.
- 2026-10-10_2142: BL-2009 re-measured at e52f2c111 (not a gap run, so the finding stays open). The in-process harness passes test1642, test1643 and test3036; Measure-ReferenceCrossCheck.ps1 leaves 1642 and 1643 out (reply-not-one-data), so both are match. test3036 is still a gap from the cross-check, not the harness, three runs of three: both binaries exit 23 with the same request bytes, but on the retry the reference curl (Git for Windows' curl 8.21.0) ends "curl: (23) client returned ERROR on write of 16 bytes" where Curl's Release build says "write of 128 bytes" (the first attempt's "write of 51 bytes" agrees). Reproduce: run Measure-UpstreamCases.cs on 3036, ConvertTo-BehaviourMeasurement.ps1 on its output, then Measure-ReferenceCrossCheck.ps1 -Measurement <that> -WorkDirectory C:\Temp\gap\xcheck, and compare recorded\3036\reference\stderr.txt with recorded\3036\curl\stderr.txt. What is left is product code: the byte count Curl reports for the failed write on the retried attempt of an -OJ transfer.
