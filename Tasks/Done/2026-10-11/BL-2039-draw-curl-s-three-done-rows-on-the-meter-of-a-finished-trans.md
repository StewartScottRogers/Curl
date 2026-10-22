---
id: BL-2039
title: Draw curl's three done rows on the meter of a finished transfer whose -o file then cannot be created
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2039 — Draw curl's three done rows on the meter of a finished transfer whose -o file then cannot be created

## Goal

`curl --output missing.txt/f.txt <url>` of an empty 200 response draws the same progress-meter rows as curl 8.21.0: the zero row and then the three done rows before the meter's newline.

## Context

Found while fixing BL-1964 (AF-0145). Measured 2026-10-10 with `Record-CurlExchange.ps1` against curl 8.21.0 (mingw, Schannel): curl's stderr is the two header lines, then `\r  0 ... 0` four times (the start row and three done rows, as after a success), then `\r\n`, then `Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n`; exit 23. Curl draws the start row only: `CurlCommandRunner.WriteProgressAsync` finishes the meter (`FinishTransferProgress`) with the result `DeferredOutputFileStream.CompleteAsync` turned into exit 23, so `TransferProgressRecorder.Finish(false)` draws one failure update instead of the done rows. curl finishes its meter from the handler's success and fails creating the empty file after. Start at `TransferIntoOutputFileAsync` and `WriteProgressAsync` in `Curl.Console/CurlCommandRunner.cs`; the meter test to extend is `CurlCommandRunnerStartedTransferProgressMeterTests.RunAsync_StartedEmptyTransferToUnopenableOutputFile_WritesTheOpenWarningAfterTheMetersNewline`. Measure a non-empty body to an unopenable file as well before pinning it.

## Acceptance criteria

- [x] For `--output missing.txt/f.txt http://127.0.0.1:<port>/` (Record-CurlExchange.ps1's default empty 200), Curl's stderr equals curl 8.21.0's byte for byte apart from timing-dependent fields.
- [x] A unit test in `Curl.Console.UnitTests` pins the four rows, the newline and the warning.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `TransferIntoOutputFileAsync` sets `RunningTransferState.SucceededBeforeOutputFileCreationFailed` when the handler succeeded and only `DeferredOutputFileStream.CompleteAsync` turned the result into exit 23; `FinishTransferProgress` then finishes the meter as a success (three done rows). Pinned by `CurlCommandRunnerStartedTransferProgressMeterTests.RunAsync_ReportedEmptyTransferToUnopenableOutputFile_DrawsTheThreeDoneRowsBeforeTheOpenWarning`.
- Measured 2026-10-10, Record-CurlExchange.ps1 against curl 8.21.0 and the built Curl: the empty 200 case is now byte-identical (stderr and exit 23).
- Non-empty body (5 bytes) measured too: curl prints the warning right after the zero row, before the meter's newline, then `curl: (23) client returned ERROR on write of 5 bytes`; Curl puts the newline before the warning. Different cause (failed body write, not empty-file creation), so filed as BL-2040 rather than widened here.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. An empty 200 to an unopenable -o file draws curl's zero row and three done rows before the newline and warning, byte for byte
