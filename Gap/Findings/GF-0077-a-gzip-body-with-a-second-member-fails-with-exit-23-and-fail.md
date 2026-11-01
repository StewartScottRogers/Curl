---
id: GF-0077
title: A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'
area: behaviour
platform: windows
key: behaviour:multi-member-gzip-error-message
severity: Medium
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test2305]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2169
tasks: [BL-2169]
---
# GF-0077 - A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'

## Summary

Curl differs from upstream curl in behaviour: A gzip body with a second member fails with exit 23 and 'Failed writing received data to disk/application' where curl says 'Multi-member gzip response not supported'.

## Evidence

behaviour:test2305 (--compressed --silent --show-error, two concatenated gzip members): expected 'reference curl exits 23; stdout 0 bytes', actual '<verify><stderr> differs at byte 11 (line 1): expected curl: (23) Multi-member gzip response not supported, got curl: (23) Failed writing received data to disk/application'. The exit code matches; only the message differs. HttpContentCodingDecoder (Curl.Protocol.Http.UnitLibrary/HttpContentCodingDecoder.cs lines 29 and 188) stops before the second member and fails with the generic write-failure message. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2305

## Suggestion

In Curl.Protocol.Http.UnitLibrary, when HttpContentCodingDecoder finds a second gzip member after the first one's trailer, fail with exit 23 and a new HttpTransferMessages constant 'Multi-member gzip response not supported', as curl 8.22.0 does; keep 'Failed writing received data to disk/application' for other trailing bytes (zlib, Brotli) after checking each against the reference.

## Measurements

- 2026-10-13_1406: 1 of 1 items are gaps.
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

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2169.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
