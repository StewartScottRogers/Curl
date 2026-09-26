---
id: BL-121
title: Write -D/--dump-header output to standard output or a file in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-120]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-121 — Write -D/--dump-header output to standard output or a file in Curl.Console

## Goal

`Curl.Console` passes each transfer a `TransferContext.HeaderOutput` for the parsed `-D` argument: standard output for `-`, otherwise the named file, so `curl -D - file:///...` and `curl -D hd.txt file:///...` write the file handler's header lines as curl 8.21.0 does.

## Context

Found in BL-111 (2026-09-26): once BL-120 parses `-D`, nothing in `Curl.Console/CurlCommandRunner.cs` sets `HeaderOutput` (`CreateContext` builds the `TransferContext`). BL-111 then prints `curl: Failed writing headers to <file>` when that write fails; this task only delivers the output.

- `-` goes to standard output (the same `StandardOutputFailureDeferringStream` the body uses, or a decision recorded in Notes on why not).
- A named file is opened through the runner's `IFileSystem` (see `DeferredOutputFileStream` for the `-o` precedent). Measure against curl 8.21.0 what it prints when the `-D` file cannot be opened; measured in BL-111 on 2026-09-26: `curl -sS -D ro.txt -o body.txt file:///C:/Temp/bl050/ten.txt` with `ro.txt` read-only prints `curl: Failed to open ro.txt` then `curl: (23) Failed writing received data to disk/application`, exit 23.
- Curl.Console is held to 100% line and branch coverage and complexity at most 10 per method.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` runs `-D - -o body.txt file:///...` through `CurlCommandRunner` and asserts standard output holds the header lines the handler wrote.
- [ ] A test runs `-D hd.txt -o body.txt file:///...` and asserts `hd.txt` in the `InMemoryFileSystem` holds the header lines.
- [ ] A test asserts that a `-D` file which cannot be opened prints `curl: Failed to open <file>` and `curl: (23) Failed writing received data to disk/application`, exit 23, under the `-s`/`-S` rule.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
