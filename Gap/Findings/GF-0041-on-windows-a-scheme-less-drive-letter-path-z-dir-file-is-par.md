---
id: GF-0041
title: On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file
area: behaviour
key: behaviour:drive-letter-path-taken-as-url-scheme
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-08_2029
closed: 2026-10-10_0657
regression: false
items: [behaviour:test1146]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
task: BL-1851
tasks: [BL-1851]
---
# GF-0041 - On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file

## Summary

Curl differs from upstream curl in behaviour: On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file.

## Evidence

Measured: test1146 expected 'upstream test1146 passes', actual 'the --output file against <reply><data> differs at byte 0 (line 1): expected "foo\n", got the end'. The command is --proto-default file %PWD/%LOGDIR/test1146.txt; upstream expects exit 0 and the file's bytes. Reran on 6383c570 with the same result. Run directly, Curl.Console/bin/Debug/net10.0/curl.exe --proto-default file Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/t1146.txt prints 'curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535' and exits 3. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 1146

## Suggestion

In Curl.Core.UnitLibrary's UrlSchemeGuesser.HasScheme, on Windows, take a single ASCII letter followed by a colon and a forward or back slash as a drive prefix, not a scheme. AddScheme then gives it the --proto-default (or guessed) scheme, and the file handler reads 'file://Z:/dir/file' as the local path Z:/dir/file, as upstream test1146 expects (exit 0, the file's bytes as output). Pin it with an [OSCondition(OperatingSystems.Windows)] test in Curl.Core.UnitTests, and pin the off-Windows answer in another.

## Measurements

- 2026-10-08_2029: 1 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.

## Log

- 2026-10-08_2029: Opened by gap-behaviour.
- 2026-10-08_2131: Filed BL-1851.
- 2026-10-10_0657: Closed: run 2026-10-10_0657 measured every item as match or excluded.
