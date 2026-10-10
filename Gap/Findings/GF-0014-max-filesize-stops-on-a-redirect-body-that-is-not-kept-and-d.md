---
id: GF-0014
title: --max-filesize stops on a redirect body that is not kept, and does not stop a decompressed body that grows past the limit
area: behaviour
key: behaviour:max-filesize-on-redirect-and-decoded-body
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test477, behaviour:test1618]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1969
tasks: [BL-1807, BL-1969]
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

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1807.
- 2026-10-10_0756: Filed BL-1969.
