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

- [ ] The case that produces `client returned ERROR on write of 17 bytes` is identified and recorded in the task Notes.
- [ ] A new MSTest (Curl.Console.UnitTests and/or Curl.Protocol.Http.UnitTests, no network) shows a failing write callback is called once per header line and the reported byte count is that first line's length (17 bytes for that case), and existing header output bytes are unchanged.
- [ ] Add test489 (or the case identified above) to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
