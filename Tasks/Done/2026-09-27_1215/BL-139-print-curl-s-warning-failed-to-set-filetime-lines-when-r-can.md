---
id: BL-139
title: Print curl's Warning: Failed to set filetime lines when -R cannot stamp the -o file
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-079]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: FR-011
created: 2026-09-26
completed: 2026-09-26
---
# BL-139 — Print curl's Warning: Failed to set filetime lines when -R cannot stamp the -o file

## Goal

When `-R`/`--remote-time` cannot set the `-o` file's time, `curl` prints the two
`Warning:` lines curl 8.21.0 prints on stderr, and still exits 0.

## Context

- BL-079 added `Curl.Core.FileSystem.IFileTimeSetter`
  (`Curl.Core.UnitLibrary/FileSystem/IFileTimeSetter.cs`,
  `bool TrySetLastWriteTimeUtc(string path, DateTimeOffset lastWriteTimeUtc)`), implemented
  by `PhysicalFileSystem` (`Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs`), which
  returns `false` on any exception `FileOpenFailure.IsOpenFailure` accepts, for example a
  missing file.
- `Curl.Console/CurlCommandRunner.cs`, `TransferToOutputFileAsync`, calls it under `-R`
  after the `-o` file is closed, when the result is a success with a non-null
  `SourceLastWriteTimeUtc`, and discards the return value with a comment saying the
  warning is not modelled yet. That is the gap.
- Measured on curl 8.21.0 (Windows, Schannel build, 2026-09-26), source file mtime
  2020-01-02 03:04:05.678 local:
  `curl -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/src.txt` (unmet condition, `out2.txt`
  did not exist) exits 0, creates no `out2.txt`, and after the progress meter lines writes
  exactly these two lines to stderr:
  ```
  Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: 
  Warning: GetLastError 0x00000002
  ```
  The first line ends in `failed: ` with a trailing space. `1577959445` is the source mtime
  as Unix seconds (whole seconds, truncated). `0x00000002` is `ERROR_FILE_NOT_FOUND`.
- Today a missing file cannot be reached end to end: `-z` is not parsed (BL-138) and an
  unmet `-z` still creates an empty `-o` file (BL-136). Unit tests reach the path with a
  fake `IFileTimeSetter` that returns failure; no dependency on those tasks is needed.
- The `GetLastError` line is Windows-specific. On POSIX, upstream's `tool_filetime.c`
  uses `utimes`/`utime` and reports `strerror`, in a form this repository has not
  measured.
- A `bool` return cannot say which Win32 error occurred. To print `0x%08x` for failures
  other than a missing file (access denied is `0x00000005`), `IFileTimeSetter` may need to
  return the error code (for example `int?` or a small result record), which is why this
  task touches `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`.
- Existing warning lines go through the runner's warning writer (see the `Warning: `
  handling near `CurlCommandRunner.cs` line 640 and `WarningLineWrapper`); use the same
  path so wrapping and `-s` behave like every other warning.
- Upstream reference: https://curl.se/docs/manpage.html#-R (checked against curl 8.21.0).

## Acceptance criteria

- [x] With a fake `IFileTimeSetter` that fails for a missing file, a test in
      `Curl.Console.UnitTests` (for example in `CurlCommandRunnerRemoteTimeTests.cs`) named
      `RunAsync_RemoteTimeCannotSetMissingFile_PrintsFailedToSetFiletimeWarnings` asserts
      stderr ends with exactly
      `Warning: Failed to set filetime 1577959445 on outfile: CreateFile failed: ` +
      newline + `Warning: GetLastError 0x00000002` + newline (trailing space kept), for a
      source time of 1577959445 Unix seconds, and the exit code is `CurlExitCode.Ok`.
- [x] The Unix seconds in the first line are the source time truncated to whole seconds;
      a test with a fractional-second source time proves it.
- [x] Either `IFileTimeSetter` reports the Win32 error code and `PhysicalFileSystem`
      supplies it (with `Curl.Core.UnitTests` covering a missing file giving 2), and the
      second line prints it as `0x` plus eight lowercase hex digits; or the task records in
      `Notes` why the code is fixed at `0x00000002` and files a follow-up task for other
      codes. Either way the choice is written in `Notes`.
- [x] Whether `-s` (and `-s -S`) suppresses these two lines on curl 8.21.0 is measured,
      recorded in `Notes` with the command used, and a test asserts the measured behaviour.
- [x] The POSIX form is either measured on a Linux curl 8.21.0 and implemented for
      non-Windows, or the Windows form is kept on every platform and that choice, with the
      reason, is recorded in `Notes` and in the XML remarks on the method that prints it.
- [x] A successful stamp prints nothing extra (an existing test or a new one asserts it).
- [x] The comment in `TransferToOutputFileAsync` saying the warning is not modelled is gone.
- [x] `dotnet build Curl.Console -warnaserror` and `dotnet build Curl.Core.UnitLibrary -warnaserror`
      are clean; `dotnet test --filter "TestCategory!=Integration"` passes;
      `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` (and
      `-Library Curl.Core` if Core changed) reports no new failing member.

## Notes

- Measured case to reproduce by hand: create a source file, then
  `curl -R -z "1 Jan 2030" -o out2.txt file:///<path>` where `out2.txt` does not exist.
- Do not widen this task to stop an unmet `-z` creating the file (BL-136) or to parse `-z`
  (BL-138).
- The two lines are one curl warning wrapped at the default 79 columns: the message is
  `Failed to set filetime <s> on outfile: CreateFile failed: GetLastError 0x%08x`, and
  curl's `warnf` wrapping splits it after `failed: `, keeping the trailing space and
  repeating `Warning: `. So the runner writes one line through `WriteErrorLineAsync` and
  `WarningLineWrapper` does the split, exactly as for every other warning
  (`Curl.Console/RemoteTimeFailureWarning.cs`).
- Error code (criterion 3, first option): `IFileTimeSetter.TrySetLastWriteTimeUtc` gained
  `out int errorCode`. `PhysicalFileSystem` supplies it through the new
  `FileOpenFailure.Win32ErrorCodeOf`: the low word of an `HRESULT_FROM_WIN32` `HResult`
  (0x8007xxxx, so a missing file gives 2, access denied 5), or the raw `HResult` when the
  exception carries no Win32 code, since no better value is known. The line always names
  `CreateFile`: `File.SetLastWriteTimeUtc` does not say whether the open or the stamp
  failed, and the open is the step that fails for missing or refused files.
- `-s` measured 2026-09-26 on curl 8.21.0 (Windows, Schannel), in `Z:	mpl139` with
  `src.txt` touched to 2020-01-02 03:04:05.678:
  `curl -s -R -z "1 Jan 2030" -o out2.txt file:///Z:/tmp/bl139/src.txt` and the same with
  `-s -S` both exit 0 with empty stderr; without `-s` the two lines appear. `-S` restores
  errors, not warnings. Test: `RunAsync_RemoteTimeCannotSetUnderSilent_PrintsNothing`.
- POSIX: not measured (no Linux curl 8.21.0 here). The Windows form is kept on every
  platform rather than guessing `strerror` text; recorded in the XML remarks of
  `RemoteTimeFailureWarning` and `CurlCommandRunner.StampOutputFileTimeAsync`.
- No ADR: these choices are the ones this task's criteria direct to Notes and XML remarks,
  and `Documentation` is outside this task's `touches`.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Console` and
  `-Library Curl.Core.UnitLibrary` both 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -R prints curl's two Failed to set filetime warning lines when it cannot stamp the -o file, muted by -s and -s -S, exit 0
