---
id: GF-0018
title: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "
area: behaviour
key: behaviour:multipart-form-encoding
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test277, behaviour:test669, behaviour:test1133, behaviour:test1315]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1811
tasks: [BL-1811]
---
# GF-0018 - -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; "

## Summary

Curl differs from upstream curl in behaviour: -F multipart bodies differ: custom Content-Type, several files in one -F, quoted file names with , ; ".

## Evidence

Every item expects 'upstream test<N> passes'. test277 (-F name=daniel -H 'Content-Type: text/info'): <verify><protocol> differs at byte 93 (line 5): expected 'Content-Length: 158', got 'Content-Type: text/info'. test669 (-H 'Content-type: multipart/form-data; charset=utf-8'): expected 'Content-Length: 260', got 'Content-type: multipart/form-data; charset=utf-8'. test1133 (quoted file name with ',', ';', '"'): request differs at byte 634 from the reference curl. test1315 (-F 'file=@a,b;type=magic/content,c'): request differs at byte 390 from the reference curl. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 277,669,1133,1315

## Suggestion

In Curl.Cli.UnitLibrary's -F parser and the multipart writer it feeds: merge a user Content-Type into the generated one (keep the user's type, append '; boundary=...', placed after Content-Length, as curl's header order shows). Parse '@a,b,c' with per-file ';type=' as several file parts. Parse a quoted file name holding ',', ';' and '"' and escape it in Content-Disposition as curl 8.21.0 does.

## Measurements

- 2026-10-08_1640: 4 of 4 items are gaps.
- 2026-10-08_2029: 3 of 4 items are gaps.
- 2026-10-10_0657: 3 of 4 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1811.
