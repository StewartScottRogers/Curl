---
id: BL-3945
title: CGR-016 part 7: Linux and macOS check --skip-existing and --remove-on-error on output names holding invalid UTF-8 argument bytes under those raw bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3868]
touches: [Curl.Console/PhysicalOutputPaths.cs, Curl.Console/RawByteFileName.cs, Curl.Console.UnitTests/Cgr016SkipExistingAndRemoveTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3945 — CGR-016 part 7: Linux and macOS check --skip-existing and --remove-on-error on output names holding invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `--skip-existing -o $'\xfc.txt' URL` skips the transfer when the file FC 2E 74 78 74 is there, and `--remove-on-error -o $'\xfc.txt'` deletes that file after a failure, as curl 8.18.0 does (today both look at EF BF BD 2E 74 78 74).

## Context

Split from BL-3868 (CGR-016, ADR-0920, ADR-0942). `-o`, `-D` and `--create-dirs` now use a name's raw bytes off Windows, but `PhysicalOutputPaths.Exists` (`File.Exists`/`Directory.Exists`), `RemoveFile` (`File.Delete`) and `ParentIsRegularFile` still go through .NET's file APIs, which encode a lone surrogate U+DC80..U+DCFF as EF BF BD. Measure curl 8.18.0 first with `Record-CurlExchange.ps1 -Curl wsl.exe` (a `file:///etc/hostname` URL needs no server; `file:///nonexistent` fails). Then, off Windows and only for a name holding an escaped byte, check existence and delete through the runtime's `SystemNative_Stat`/`SystemNative_Unlink` (libSystem.Native, non-variadic) beside `RawByteFileName.MakeDirectoryUnderRawBytes`, injected into `PhysicalOutputPaths` as BL-3868 injected the directory maker. Check on Linux with a `dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false -p:PublishSingleFile=false` build run from WSL.

## Acceptance criteria

- [ ] `--skip-existing -o $'\xfc.txt'` with that raw-named file present skips the transfer as curl 8.18.0 does on Linux; the measured command and both results are recorded in Notes.
- [ ] `--remove-on-error -o $'\xfc.txt' file:///nonexistent` leaves or removes the raw-named file as curl 8.18.0 does on Linux.
- [ ] Every other name, and every name on Windows, behaves as before.
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr016SkipExistingAndRemoveTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
