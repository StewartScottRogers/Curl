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
completed:
---
# BL-2039 — Draw curl's three done rows on the meter of a finished transfer whose -o file then cannot be created

## Goal

`curl --output missing.txt/f.txt <url>` of an empty 200 response draws the same progress-meter rows as curl 8.21.0: the zero row and then the three done rows before the meter's newline.

## Context

Found while fixing BL-1964 (AF-0145). Measured 2026-10-10 with `Record-CurlExchange.ps1` against curl 8.21.0 (mingw, Schannel): curl's stderr is the two header lines, then `\r  0 ... 0` four times (the start row and three done rows, as after a success), then `\r\n`, then `Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n`; exit 23. Curl draws the start row only: `CurlCommandRunner.WriteProgressAsync` finishes the meter (`FinishTransferProgress`) with the result `DeferredOutputFileStream.CompleteAsync` turned into exit 23, so `TransferProgressRecorder.Finish(false)` draws one failure update instead of the done rows. curl finishes its meter from the handler's success and fails creating the empty file after. Start at `TransferIntoOutputFileAsync` and `WriteProgressAsync` in `Curl.Console/CurlCommandRunner.cs`; the meter test to extend is `CurlCommandRunnerStartedTransferProgressMeterTests.RunAsync_StartedEmptyTransferToUnopenableOutputFile_WritesTheOpenWarningAfterTheMetersNewline`. Measure a non-empty body to an unopenable file as well before pinning it.

## Acceptance criteria

- [ ] For `--output missing.txt/f.txt http://127.0.0.1:<port>/` (Record-CurlExchange.ps1's default empty 200), Curl's stderr equals curl 8.21.0's byte for byte apart from timing-dependent fields.
- [ ] A unit test in `Curl.Console.UnitTests` pins the four rows, the newline and the warning.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
