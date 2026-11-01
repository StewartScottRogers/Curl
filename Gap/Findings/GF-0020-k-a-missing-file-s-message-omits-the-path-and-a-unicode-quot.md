---
id: GF-0020
title: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
area: behaviour
platform: windows
key: behaviour:config-file-reading
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-11_1950
regression: false
items: [behaviour:test411, behaviour:test470]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2078
tasks: [BL-1813, BL-1973, BL-2078]
---
# GF-0020 - -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently

## Summary

Curl differs from upstream curl in behaviour: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently.

## Evidence

Both items expect 'upstream test<N> passes'. test411 (-K <LOGDIR>/missing): <verify><stderr> differs at byte 30 (line 1): expected "curl: cannot read config from '<LOGDIR>/missing'", got 'curl: cannot read config from '. test470 (config file with a Unicode quote character): request differs at byte 77 from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 411,470

## Suggestion

In Curl.Cli.UnitLibrary's config-file reader: put the quoted path in 'cannot read config from '<path>''. Read a Unicode quote character (U+201C/U+201D) in a config value as curl 8.21.0 does: keep the bytes literally and write its warning, so the request matches the reference.

## Measurements

- 2026-10-08_1640: 2 of 2 items are gaps.
- 2026-10-08_2029: 2 of 2 items are gaps.
- 2026-10-10_0657: 1 of 2 items are gaps.
- 2026-10-11_0936: 1 of 2 items are gaps.
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
- 2026-10-31_1828: 0 of 2 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1813.
- 2026-10-10_0756: Filed BL-1973.
- 2026-10-11_1029: Filed BL-2078.
- 2026-10-11_1950: Closed: run 2026-10-11_1950 measured every item as match or excluded.
