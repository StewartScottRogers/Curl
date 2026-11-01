---
id: GF-0078
title: The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .)
area: behaviour
platform: windows
key: behaviour:progress-meter-lines-differ-from-reference
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test675, behaviour:test1498]
touches: [Curl.Console, Curl.Console.UnitTests]
task: BL-2170
tasks: [BL-2170]
---
# GF-0078 - The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .)

## Summary

Curl differs from upstream curl in behaviour: The progress meter on stderr ends its lines differently from the reference curl (an extra restarted line for a second URL, a final line feed after -T .).

## Evidence

Both pass in process and differ only out of process (evidence 'was match; in-process and out-of-process differ', actual 'stderr differs'). test675 (two URLs with different user:password, reply '200 OK swsclose'): expected 'reference curl exits 0; stdout 136 bytes: HTTP/1.1 200 OK swsclose ...'; the reference stderr for the second transfer holds an extra zero meter line ended by CR LF before its '100 26 100 26' lines, Curl's has none. test1498 (http://%HOSTIP:%HTTPPORT/bzz/1498 -T .): expected 'reference curl exits 0; stdout 0 bytes'; the reference stderr ends after its last meter update with no line feed, Curl's ends with CR LF. Recorded under C:/curl-crosscheck/recorded/675 and /1498 (reference/stderr.txt against curl/stderr.txt). Reproduce: powershell -NoProfile -File Gap/Tools/Measure-ReferenceCrossCheck.ps1 -Measurement <run>/behaviour-unchecked.json -TestsData $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data -Candidate <run>/tree/Curl.Console/bin/Release/net10.0/curl.exe -OutFile $env:TEMP/behaviour.json, then compare each case's reference/stderr.txt with curl/stderr.txt.

## Suggestion

In Curl.Console (ProgressMeterLines and the meter's line endings), match the reference curl: for test675, end the meter line and start a new one when the transfer restarts (the second URL's request on the first connection, which the server closed, retried on a new one); for test1498, leave the final line feed off when the '-T .' upload ends as the reference does. Measure both several times on the reference with Record-CurlExchange.ps1 first, to separate a fixed rule from timing.

## Measurements

- 2026-10-13_1406: 2 of 2 items are gaps.
- 2026-10-14_0904: 0 of 2 items are gaps.
- 2026-10-15_0304: 0 of 2 items are gaps.
- 2026-10-15_0543: 0 of 2 items are gaps.
- 2026-10-15_0751: 0 of 2 items are gaps.
- 2026-10-15_1657: 0 of 2 items are gaps.
- 2026-10-17_2225: 0 of 2 items are gaps.
- 2026-10-19_0804: 0 of 2 items are gaps.
- 2026-10-19_1329: 0 of 2 items are gaps.
- 2026-10-22_0148: 0 of 2 items are gaps.
- 2026-10-23_1637: 0 of 2 items are gaps.
- 2026-10-25_1542: 0 of 2 items are gaps.
- 2026-10-30_2103: 0 of 2 items are gaps.
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2170.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
