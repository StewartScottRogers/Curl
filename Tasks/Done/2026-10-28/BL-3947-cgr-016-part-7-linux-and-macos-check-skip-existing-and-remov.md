---
id: BL-3947
title: CGR-016 part 7: Linux and macOS check --skip-existing and --remove-on-error on output names holding invalid UTF-8 argument bytes under those raw bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3868]
touches: [Curl.Console/PhysicalOutputPaths.cs, Curl.Console/RawByteFileName.cs, Curl.Console.UnitTests/Cgr016SkipExistingAndRemoveTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3947 — CGR-016 part 7: Linux and macOS check --skip-existing and --remove-on-error on output names holding invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `--skip-existing -o $'\xfc.txt' URL` skips the transfer when the file FC 2E 74 78 74 is there, and `--remove-on-error -o $'\xfc.txt'` deletes that file after a failure, as curl 8.18.0 does (today both look at EF BF BD 2E 74 78 74).

## Context

Split from BL-3868 (CGR-016, ADR-0920, ADR-0943). `-o`, `-D` and `--create-dirs` now use a name's raw bytes off Windows, but `PhysicalOutputPaths.Exists` (`File.Exists`/`Directory.Exists`), `RemoveFile` (`File.Delete`) and `ParentIsRegularFile` still go through .NET's file APIs, which encode a lone surrogate U+DC80..U+DCFF as EF BF BD. Measure curl 8.18.0 first with `Record-CurlExchange.ps1 -Curl wsl.exe` (a `file:///etc/hostname` URL needs no server; `file:///nonexistent` fails). Then, off Windows and only for a name holding an escaped byte, check existence and delete through the runtime's `SystemNative_Stat`/`SystemNative_Unlink` (libSystem.Native, non-variadic) beside `RawByteFileName.MakeDirectoryUnderRawBytes`, injected into `PhysicalOutputPaths` as BL-3868 injected the directory maker. Check on Linux with a `dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false -p:PublishSingleFile=false` build run from WSL.

## Acceptance criteria

- [x] `--skip-existing -o $'\xfc.txt'` with that raw-named file present skips the transfer as curl 8.18.0 does on Linux; the measured command and both results are recorded in Notes.
- [x] `--remove-on-error -o $'\xfc.txt' file:///nonexistent` leaves or removes the raw-named file as curl 8.18.0 does on Linux.
- [x] Every other name, and every name on Windows, behaves as before.
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr016SkipExistingAndRemoveTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured curl 8.18.0 on Linux (`Record-CurlExchange.ps1 -NoServer -Curl wsl.exe -CurlArgs '-d','Ubuntu','--','bash',<script>`), in a directory holding FC 2E 74 78 74 ("old"): `curl -v --skip-existing -o $'\xfc.txt' file:///etc/hostname -w 'skip=%{http_code}\n'` prints `Note: skips transfer, "\xfc.txt" exists locally` and `skip=000`, and the file keeps "old". `curl -v --remove-on-error -o $'\xfc.txt' file:///nonexistent` gives `curl: (37) Could not open file /nonexistent` and leaves the file, because the transfer never opened it. `curl -v --remove-on-error -o $'\xfd.txt' --max-filesize 1 file:///etc/hostname`, with FD 2E 74 78 74 there, gives `curl: (63) ...` and `Note: Removed output file: \xfd.txt`, and the file is gone.
- Curl, published for linux-x64 (`dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false -p:PublishSingleFile=false`) and run from WSL with the same script, gives the same lines byte for byte and leaves the same files. Before this change the check and the delete looked at EF BF BD 2E 74 78 74.
- Change: `RawByteFileName.FileOperations` (`FileTypeOf` gives the `S_IFMT` bits from `SystemNative_Stat`, or -1; `Remove` is `SystemNative_Unlink`) is injected into `PhysicalOutputPaths` as `rawByteFiles`, and only off Windows (`OffWindows<T>` replaces `RawDirectoryMakerForCurrentPlatform`). `Exists` and `RemoveFile` use it only for a name holding an escaped byte. A raw-named path whose stat is not a regular file gives `NotRegularFile`, as `NativeRegularFileTest` does for every other name. Every other name, and every name on Windows, goes through the same code as before.
- Decision (sensible default, no new ADR): this applies ADR-0920's and ADR-0943's raw-byte rule to two more calls, using the same injected-delegate seam BL-3868 used. There is no new behaviour choice to record.
- Gates: `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members. `dotnet build Curl.slnx` has 0 warnings. The fast tests pass (Curl.Console.UnitTests: 3011 passed, 31 skipped). The new tests use fake delegates and ordinary temp files, so they are platform-neutral, and their pre-change failure follows from `File.Exists` being false for an unpaired surrogate name.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d0d06f1b b140d9ba 1b81d9c1 92e2d50f 3ccadb6a 779790c0.
- 2026-10-28: Doing -> Done. --skip-existing and --remove-on-error check and delete raw-byte output names under their raw bytes on Linux and macOS
