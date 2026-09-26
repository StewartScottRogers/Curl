---
id: BL-087
title: Print curl's Failed to open the file warning when an -o file cannot be created
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-068]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-087 — Print curl's Failed to open the file warning when an -o file cannot be created

## Goal

When an `-o` file cannot be created and `-s` is not given, `Curl.Console` prints curl
8.21.0's `Warning: Failed to open the file <path>: <reason>` line on standard error before
the exit 23 line, as upstream does.

## Context

- BL-068 added `Curl.Console/DeferredOutputFileStream.cs`, which opens the `-o` file on
  the first write and turns a failed open into curl's
  `curl: (23) client returned ERROR on write of N bytes`. It prints no warning.
- Measured 2026-09-26 with the local curl 8.21.0 (x86_64-w64-mingw32):
  `curl -o Z:/nonexist/x file:///C:/Windows/win.ini` prints (after the progress meter)
  `Warning: Failed to open the file Z:/nonexist/x: No such file or directory`, then an
  empty line, then `curl: (23) client returned ERROR on write of 92 bytes`, and exits 23.
  Under `-s` or `-sS` the warning is not printed.
- The `<reason>` is the C runtime's `strerror` text for the failed `fopen`. Today
  `IFileSystem.OpenForWriteAsync` reports only a `FileAccessStatus` (`NotFound`,
  `IsDirectory`, `AccessDenied`, `IoError`); map each to curl's text on Windows
  (`No such file or directory`, `Permission denied`, …), measuring each against the
  local curl first. The blank line after the warning must be measured too: it may come
  from the progress meter, which is not implemented.
- A successful empty transfer whose `-o` file cannot be created exits 23 with no line
  under `-sS`; check what curl prints for it without `-s`.

## Acceptance criteria

- [ ] A named test asserts the warning line, byte for byte against the measured text,
      when an `-o` file cannot be created and `-s` is not given.
- [ ] A named test asserts no warning under `-s` and under `-sS`.
- [ ] The `FileAccessStatus` to reason-text mapping is recorded in `Notes` with the curl
      command used to measure each value.
- [ ] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
