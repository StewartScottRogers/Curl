---
id: BL-2040
title: Print the -o open warning before the meter's newline when a body write cannot open the file
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2040 — Print the -o open warning before the meter's newline when a body write cannot open the file

## Goal

`curl --output missing.txt/f.txt <url>` of a 5-byte 200 response writes the same stderr as curl 8.21.0: the `Warning: Failed to open the file` line straight after the zero row, before the meter's newline.

## Context

Found while doing BL-2039. Measured 2026-10-10 with `Record-CurlExchange.ps1 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs '--output','missing.txt/f.txt','http://127.0.0.1:<port>/'` against curl 8.21.0 (mingw, Schannel): stderr is the two header lines, then `\r  0 ... 0` (the zero row, no line ending), then `Warning: Failed to open the file missing.txt/f.txt: No such file or directory\r\n`, then `\r\n` (the meter's newline), then `curl: (23) client returned ERROR on write of 5 bytes\r\n`; exit 23. Curl writes the zero row, `\r\n`, the warning, then the `curl: (23)` line - no empty line. curl prints the warning when the first body write fails, while the meter is still on its row. BL-1964 holds the warning until after the meter's newline (`WriteOrHoldOutputFileOpenWarningAsync` in `Curl.Console/CurlCommandRunner.cs`), which is right for the empty body (BL-2039) but not for a failed body write. Start at `DeferredOutputFileStream`'s first-write open failure and `WriteProgressAndAbandonedRetryWarningAsync`.

## Acceptance criteria

- [ ] For the 5-byte case above, Curl's stderr equals curl 8.21.0's byte for byte apart from timing-dependent fields.
- [ ] The empty-body case of BL-2039 (`CurlCommandRunnerStartedTransferProgressMeterTests`) still passes.
- [ ] A unit test in `Curl.Console.UnitTests` pins the zero row, the warning, the newline and the `curl: (23)` line.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-10: Created.
