---
id: BL-103
title: Stop the remaining URLs when a resumed -o file cannot be opened in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-095]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-103 — Stop the remaining URLs when a resumed -o file cannot be opened in Curl.Console

## Goal

When `-C` resumes into an `-o` file that cannot be opened, Curl.Console stops the whole run with exit 23 and transfers none of the remaining URLs, as curl 8.21.0 does.

## Context

Found while finishing BL-095. Since BL-095, `CurlCommandRunner.TransferToOutputFileAsync` (`Curl.Console/CurlCommandRunner.cs`) opens the `-o` file for appending before a resumed transfer and, on failure, prints `curl: cannot open '<path>'` then `curl: (23) Failed writing received data to disk/application`. It then carries on to the next URL. curl does not.

Measured with local curl 8.21.0 (x86_64-w64-mingw32) on Windows, 2026-09-26, against a ten-byte `file://` source: `curl --no-progress-meter -C 3 URL URL -o d -o o9.txt`, where `d` is an existing directory, prints `curl: cannot open 'd'` and the `(23)` line on stderr, exits 23, and never transfers the second URL (`o9.txt` is not created).

Start at the loop in `CurlCommandRunner` that walks the URLs and the branch in `TransferToOutputFileAsync` that writes the `cannot open` line (around lines 307-350). The existing tests for this path are in `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs`; `Curl.Console.UnitTests/InMemoryFileSystem.cs` is the fake file system they use. Only the cannot-open-for-resume failure stops the run; do not change how other per-URL failures carry on.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests/CurlCommandRunnerTransferOptionTests.cs` runs two URLs with `-C 3` and two `-o` files, the first of which cannot be opened, and pins that the second URL's handler is never called, no second output file is created, and the exit code is 23 (`CurlExitCode.WriteError`).
- [ ] The same test pins stderr as exactly `curl: cannot open 'd'` followed by `curl: (23) Failed writing received data to disk/application` (with curl's line endings as the existing tests assert them).
- [ ] `dotnet build Curl.Console -warnaserror` is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green, and no new test needs `TestCategory=Integration`.

## Notes

## Log

- 2026-09-26: Created.
