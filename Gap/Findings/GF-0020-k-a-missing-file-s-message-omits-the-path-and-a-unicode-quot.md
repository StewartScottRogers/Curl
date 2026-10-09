---
id: GF-0020
title: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
area: behaviour
key: behaviour:config-file-reading
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test411, behaviour:test470]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1813
tasks: [BL-1813]
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

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1813.
