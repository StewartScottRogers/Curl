---
id: GF-0072
title: --next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs)
area: behaviour
platform: windows
key: behaviour:connection-reuse-match-differs
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test2051, behaviour:test3225]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
task: BL-2164
tasks: [BL-2164]
---
# GF-0072 - --next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs)

## Summary

Curl differs from upstream curl in behaviour: --next transfers reuse a connection where curl opens a new one (another host name at the same --resolve address) and open one where curl reuses (only --connect-to differs).

## Evidence

behaviour:test2051 (three --next GETs of one URL, the first with --connect-to ::%HOSTIP:%HTTPPORT, each -w '%{num_connects}<LF>'): expected 'reference curl exits 0; stdout 111 bytes: ... OK 1 OK 0 OK 0', actual '<verify><stdout> differs at byte 104 (line 9): expected 0<LF>, got 1<LF>' - the second group opens a new connection where the reference curl reuses the first. behaviour:test3225 (host-a.example.test, then after --next host-b.example.test, both mapped by --resolve to %HOSTIP, with -o): expected 'upstream test3225 passes', actual '<verify><protocol> differs at byte 201 (line 12): expected [DISCONNECT]<LF>, got the end' - the server sees one connection close where upstream opens and closes two. OptionGroupConnectionSettings (Curl.Console/OptionGroupConnectionSettings.cs) carries the --connect-to entries verbatim, so groups that differ only there never share. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2051,3225

## Suggestion

Align the connection match with curl 8.22.0's. In Curl.Console's OptionGroupConnectionSettings, drop the --connect-to entries from the compared settings, since ConnectionPoolKey already holds the host and port dialled (test2051: the mapping to the same host:port leaves the second and third groups on the first connection, num_connects 0). In Curl.Networking.UnitLibrary's ConnectionPoolKey and PoolingConnector, make the key's Host the URL's host name, never the --resolve address, so host-a and host-b at one address get one connection each (test3225, 'connects=1/connects=1/'). Rerun test3225 with --keep-logs first to confirm Curl reuses the connection rather than leaving the second one unclosed.

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

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2164.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
