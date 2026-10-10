---
id: GF-0011
title: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read
area: behaviour
key: behaviour:url-option-reads-at-file
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test488, behaviour:test489, behaviour:test2012]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2004
tasks: [BL-1804, BL-2004]
---
# GF-0011 - --url @file and --url @- are taken as a literal URL instead of a list of URLs to read

## Summary

Curl differs from upstream curl in behaviour: --url @file and --url @- are taken as a literal URL instead of a list of URLs to read.

## Evidence

Every item expects 'upstream test<N> passes'. test488 (--url @-) actual: <verify><protocol> differs at byte 5 (line 1): expected 'GET /a HTTP/1.1', got 'GET / HTTP/1.1'. test489 (--url @%LOGDIR/urls): expected 'GET /a HTTP/1.1', got 'GET /repos/Curl.gap/.../urls HTTP/1.1'. test2012: expected 'PUT /2012 HTTP/1.1', got 'PUT /repos/Curl.gap/.../urls HTTP/1.1'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 488,489,2012

## Suggestion

In Curl.Cli.UnitLibrary's --url option (CommandLineOptionTable / its applier), support curl 8.21.0's '@file' and '@-' forms: read the file or stdin, add one URL per non-blank line, and pair them with -o/--output and -T in order. Keep --ai-help's url entry right.

## Measurements

- 2026-10-08_1640: 3 of 3 items are gaps.
- 2026-10-08_2029: 3 of 3 items are gaps.
- 2026-10-10_0657: 2 of 3 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1804.
- 2026-10-10_0756: Filed BL-2004.
