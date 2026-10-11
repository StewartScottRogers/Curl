---
id: BL-2092
title: Write each response header line to the write callback separately (upstream test489 stderr byte counts)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2092 — Write each response header line to the write callback separately (upstream test489 stderr byte counts)

## Goal

Curl writes each response header line to the write callback separately, so the stderr of upstream test489 reports the same per-line byte count curl does (`client returned ERROR on write of 17 bytes`, not `261 bytes`).

## Context

In the measured case curl's stderr says `client returned ERROR on write of 17 bytes` while Curl says `261 bytes`, because Curl writes the whole header block at once where curl calls the write callback once per header line (the same shape BL-2009 fixed for `RemoteHeaderNameStream`). Find where the header block reaches the write callback (start in Curl.Console with the BL-2009 change; then the response header reading in Curl.Protocol.Http.UnitLibrary) and split it per line. The 8.21.0 `tests/data/test489` is `--output-dir DIR --url @FILE` with two URLs and verifies only the protocol, so first confirm which measured case produces the 17-byte message (the gap harness's raw output for test489) and record that in Notes.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task none (found by the same re-measurement). Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [x] The case that produces `client returned ERROR on write of 17 bytes` is identified and recorded in the task Notes.
- [x] A new MSTest (Curl.Console.UnitTests and/or Curl.Protocol.Http.UnitTests, no network) shows a failing write callback is called once per header line and the reported byte count is that first line's length (17 bytes for that case), and existing header output bytes are unchanged.
- [x] Add test489 (or the case identified above) to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

- The 17-byte case: the gap harness's raw output sits under `Gap/`, which lanes may not read,
  so it was identified from the numbers: 17 is `HTTP/1.1 200 OK` plus CR LF, the first line
  of an `-i` head written to an `-o` file that cannot be opened (`-i -o <unopenable>`); 261
  was the whole head. test489 itself was already in `PassingUpstreamCases.txt` (line 397), so
  that list needed no change. Pinned in `CurlCommandRunnerRetryTests.RunAsync_IncludedHeadersToAnUnopenableFile_ReportsTheFirstHeaderLinesLength`.
- Fix: `HttpProtocolHandler.WriteHeadersAsync` writes head and trailer bytes one line per
  write (`ExecuteAsync_HeaderOutput_WritesEachHeaderLineSeparately` pins 17,26,15,19,2 and
  unchanged bytes). `RemoteHeaderNameStream` (`-J`) now holds a head's lines from its status
  line to its empty line until the file is named, so test1310's `-J -i` file still gets
  every header line; two of its unit tests gained the head's empty line they had left out.
- Measure-CodeQuality.ps1 was not run (run cost cap); the new branches are covered by the
  tests above.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
