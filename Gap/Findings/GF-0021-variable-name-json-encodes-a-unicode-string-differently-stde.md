---
id: GF-0021
title: --variable ... {{name:json}} encodes a Unicode string differently (stderr)
area: behaviour
key: behaviour:variable-json-function-unicode
severity: Medium
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test268]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-1814
tasks: [BL-1814]
---
# GF-0021 - --variable ... {{name:json}} encodes a Unicode string differently (stderr)

## Summary

Curl differs from upstream curl in behaviour: --variable ... {{name:json}} encodes a Unicode string differently (stderr).

## Evidence

behaviour:test268 expected 'upstream test268 passes' (the reference curl exits 0), actual 'stderr differs' from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 268

## Suggestion

In Curl.Cli.UnitLibrary's --variable / --expand-* expansion, make the ':json' function escape a Unicode string byte for byte as curl 8.21.0 does: control characters as \uXXXX, and bytes >= 0x80 passed through. Make the stderr match the reference for upstream tests/data/test268.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 1 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1814.
