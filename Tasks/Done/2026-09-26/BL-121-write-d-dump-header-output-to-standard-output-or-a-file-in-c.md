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
completed: 2026-09-26
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

- [x] A test in `Curl.Console.UnitTests` runs `-D - -o body.txt file:///...` through `CurlCommandRunner` and asserts standard output holds the header lines the handler wrote.
- [x] A test runs `-D hd.txt -o body.txt file:///...` and asserts `hd.txt` in the `InMemoryFileSystem` holds the header lines.
- [x] A test asserts that a `-D` file which cannot be opened prints `curl: Failed to open <file>` and `curl: (23) Failed writing received data to disk/application`, exit 23, under the `-s`/`-S` rule.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Plan (2026-09-26): `CurlCommandRunner.TransferWithHeaderOutputAsync` picks the header stream per URL and threads it through `TransferAsync` / `TransferToOutputFileAsync` into `CreateContext`'s `HeaderOutput`. Delivered directly in-session rather than through every `/feature` subagent stage: one method and its tests in one project.
- `-D -` uses the same `StandardOutputFailureDeferringStream` as the body, so headers and body share curl's single stdout `FILE`. Reporting a failed header write is BL-111's.
- A named `-D` file opens before the transfer through the runner's `IFileSystem`, with `DeferredOutputFileStream.CreateMode` (curl's `fopen` 0666). It is truncated for the first URL and appended for the rest, following curl's tool_operate.c ("wb" for the first transfer, then "ab"). It is closed after each transfer.
- The `-D` name is not passed through `WindowsOutputFileNameSanitizer`: curl sanitizes only `-o`/`-O` names.
- An unopenable `-D` file prints `curl: Failed to open <file>` (curl's `errorf`, so the `-s`/`-S` rule applies) and then `curl: (23) Failed writing received data to disk/application`, and stops the run. Remaining URLs are skipped, as curl's `single_transfer` breaks with CURLE_WRITE_ERROR, the same as the `-C` open-failure precedent. The `-o` file is not created.
- Not done: curl's `-D %` (headers to stderr). It is outside this task's goal and is left for a follow-up if wanted.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Console sends -D header lines to standard output for '-' or to the named file; an unopenable -D file prints 'curl: Failed to open <file>' and exits 23
