---
id: GF-0074
title: A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names
area: behaviour
platform: windows
key: behaviour:file-url-directory-not-listed-on-windows
severity: High
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test3203]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
task: BL-2166
tasks: [BL-2166]
---
# GF-0074 - A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names

## Summary

Curl differs from upstream curl in behaviour: A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names.

## Evidence

behaviour:test3203 (file://localhost%FILE_PWD/%LOGDIR/test3203.dir/ holding dir-listing-test.txt; expects exit 0 and stdout 'dir-listing-test.txt'): expected 'upstream test3203 passes', actual '<verify><stdout> differs at byte 0 (line 1): expected dir-listing-test.txt<LF>, got the end'. FileProtocolHandler.ListsDirectories (Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs line 149) is true only off Windows (OperatingSystem.IsWindows() negated), so on Windows a directory keeps exit 37; 8.22.0 changed test3203 (GF-0038) and no longer keeps it off Windows. Reproduce on Windows: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 3203

## Suggestion

In Curl.Protocol.File.UnitLibrary, list directories on Windows as curl 8.22.0 does: once the Windows reference curl 8.22.0 is measured listing a directory with Record-CurlExchange.ps1, make FileProtocolHandler.ListsDirectories true on every platform (or remove it) and have the Windows file system implement IDirectoryLister with the same entry names and order as the Linux and macOS listing. Update the Windows-only tests that pin exit 37 for a directory.

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

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2166.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
