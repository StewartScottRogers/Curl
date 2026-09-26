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
completed: 2026-09-26
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

- [x] A named test asserts the warning line, byte for byte against the measured text,
      when an `-o` file cannot be created and `-s` is not given.
- [x] A named test asserts no warning under `-s` and under `-sS`.
- [x] The `FileAccessStatus` to reason-text mapping is recorded in `Notes` with the curl
      command used to measure each value.
- [x] `dotnet build Curl.Console -warnaserror` is clean and
      `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered: `Curl.Console/OutputFileOpenWarning.cs` builds the line;
  `DeferredOutputFileStream.OpenFailureWarning` is set by any failed open (first write or
  the empty-file open in `CompleteAsync`); `CurlCommandRunner` writes it before the
  `curl: (23)` line unless `-s` was given (with or without `-S`).
- Tests: `CurlCommandRunnerTests.RunAsync_OutputFileCannotBeCreatedWithoutSilent_PrintsFailedToOpenWarningBeforeWriteLine`,
  `..._OutputFileCannotBeCreatedUnderSilent_PrintsNoWarning`,
  `..._OutputFileCannotBeCreatedUnderSilentShowError_PrintsOnlyTheWriteLine`,
  `..._EmptyTransferToUncreatableOutputFileWithoutSilent_PrintsOnlyTheWarning`;
  `OutputFileOpenWarningTests`; two `DeferredOutputFileStreamTests.OpenFailureWarning_*`.
- `FileAccessStatus` to reason, measured 2026-09-26 with curl 8.21.0 (x86_64-w64-mingw32),
  each as `curl --no-progress-meter -o <path> file:///C:/Windows/win.ini`:
  - `NotFound` -> `No such file or directory`: `-o Z:/nonexist/x`.
  - `AccessDenied` -> `Permission denied`: `-o C:/Windows/System32/bl087.txt`.
  - `IsDirectory` -> `Permission denied`: `-o <an existing directory>` (Windows `fopen`
    gives EACCES; glibc would say `Is a directory`, not handled, Windows is the target).
  - `IoError` -> `Invalid argument`: `-o C:/Windows/a:b:c` and `-o C:/nul/`.
- The blank line after the warning comes from the progress meter: with
  `--no-progress-meter` curl prints the warning and the exit 23 line with no blank line
  between. The progress meter is not implemented, so none is printed (default taken).
- Empty successful transfer to an uncreatable `-o` file, without `-s`: curl prints the
  warning only, no `curl: (23)` line, exit 23. Matched.
- Not done here, filed: curl wraps warnings at its terminal width (79 columns by default,
  so a long path wraps) -> BL-090; curl on Windows replaces `?` and `*` in the `-o` name
  with `_` before opening -> BL-091. Choice: print the warning unwrapped, as the existing
  `CommandLineWarning` lines are, since the measured acceptance case fits in 79 columns.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. curl -o to an uncreatable file prints curl's 'Warning: Failed to open the file <path>: <reason>' line before exit 23, unless -s
