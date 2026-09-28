---
id: BL-416
title: Print curl's '** Resuming transfer from byte position -1' for a -T upload with -C -
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-351]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-416 — Print curl's '** Resuming transfer from byte position -1' for a -T upload with -C -

## Goal

`curl -C - -T f.txt http://…` writes `** Resuming transfer from byte position -1` to standard error before the progress meter, as curl 8.21.0 does, measured.

## Context

- Measured in BL-351 (Notes there, ADR-0087): every `-C - -T` run of `/mingw64/bin/curl` 8.21.0 - a file, a file with `-o` naming an existing 3-byte file, an empty file, and `-T -` - wrote `** Resuming transfer from byte position -1` as the first stderr line, then the meter.
- `CurlCommandRunner.StartTransferProgress` prints the line only for a resolved offset above zero (`ProgressMeterLines`); for an upload's `-C -` the offset is `null` (or the `-o` file's size), so it prints nothing, or the wrong number.
- `TransferContextFactory.ResumesUploadFromUnknownOffset` already tells the case apart (`-C -` with a `-T` source).

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test pins stderr `** Resuming transfer from byte position -1` then the meter for `-C - -T f.txt` over HTTP, and the same with `-o` naming an existing file.
- [x] `-C 0 -T f.txt` still prints no resume line.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no new failing member in `Curl.Console`.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: a one-line behaviour change in one project, already measured in BL-351, so no plan or ADR beyond ADR-0087 was needed.
- `ProgressMeterLines.UnknownUploadOffset` (-1) is curl's own `resume_from` for this case; `HeaderLines` prints the resuming line for it as for any offset past zero.
- `CurlCommandRunner` sets `uploadResumesFromUnknownOffset` per transfer (`-C -` with a `-T` source, the same test as `TransferContextFactory.ResumesUploadFromUnknownOffset`) and `TakeProgressMeterHeaderLines` substitutes -1, so the `--retry` path gets it too. The transfer's own resume offset, and so the `-o` file's append mode, is unchanged; only the meter line is.
- Applies to every scheme, as curl's line comes from the tool/progress code rather than the protocol; only HTTP was measured.
- Tests: `CurlCommandRunnerProgressMeterTests.RunAsync_ContinueAtDashWithUpload_*` and `RunAsync_ContinueAt0WithUpload_*`. `Measure-CodeQuality.ps1`: Curl.Console's one failing member is `DiskWriteOutFileOpener.TryOpen`, already failing before this task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -C - -T writes '** Resuming transfer from byte position -1' before the meter, with or without -o
