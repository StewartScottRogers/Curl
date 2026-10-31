---
id: GF-0014
title: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit
area: behaviour
platform: windows
key: behaviour:max-filesize-on-redirect-and-decoded-body
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-11_0936
regression: false
items: [behaviour:test477, behaviour:test1618]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2007
tasks: [BL-1807, BL-2007]
---
# GF-0014 - --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit

## Summary

Curl differs from upstream curl in behaviour: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit.

## Evidence

Both items expect 'upstream test<N> passes'. test477 (--max-filesize 5 -L, 301 with a 26-byte body): <verify><protocol> differs at byte 81 (line 6): expected 'GET /4770002 HTTP/1.1', got the end. test1618 (brotli bomb, --compressed --max-filesize=1000): the --output file against <reply><data> differs at byte 121: expected the end, got NUL bytes. The reference curl exits 63. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 477,1618

## Suggestion

In Curl.Protocol.Http.UnitLibrary's HttpDownloadConditions / HttpContentLength: apply --max-filesize only to the body that is kept, not to a redirect response -L follows. Also count the decoded bytes (HttpContentDecoder) against the limit and fail with exit 63 as soon as they pass it.

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.
- 2026-10-08_2029: 1 of 2 items are gaps.
- 2026-10-10_0657: 1 of 2 items are gaps.
- 2026-10-11_0936: 0 of 2 items are gaps.
- 2026-10-11_1950: 0 of 2 items are gaps.
- 2026-10-12_0951: 0 of 2 items are gaps.
- 2026-10-12_2042: 0 of 2 items are gaps.
- 2026-10-13_1406: 0 of 2 items are gaps.
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

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1807.
- 2026-10-10_0756: Filed BL-2007.
- 2026-10-11_0936: Closed: run 2026-10-11_0936 measured every item as match or excluded.
