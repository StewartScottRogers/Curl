---
id: BL-3868
title: CGR-016 part 6: Linux and macOS open -D files and make --create-dirs directories whose names hold invalid UTF-8 argument bytes under those raw bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3853]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console/PhysicalOutputPaths.cs, Curl.Console/RawByteFileName.cs, Curl.Console.UnitTests/Cgr016HeaderFileAndDirectoryTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3868 — CGR-016 part 6: Linux and macOS open -D files and make --create-dirs directories whose names hold invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `curl -D $'\xfc.h' URL` writes the header file named FC 2E 68, and `curl --create-dirs -o $'\xfd/b.txt' URL` makes the directory FD, as curl 8.18.0 does (today EF BF BD in both).

## Context

Split from BL-3853 (CGR-016). BL-3853 (ADR-0920) opens `-o`, `-O` and `-J` files under their raw bytes through `RawByteFileName.OpenForWriteAsync`, used by `DeferredOutputFileStream`. The `-D` file is opened by `CurlCommandRunner` straight through `IFileSystem.OpenForWriteAsync`, and `--create-dirs` makes directories through `PhysicalOutputPaths.TryCreateDirectory` (`Directory.CreateDirectory`), both of which encode a lone surrogate U+DC80..U+DCFF as EF BF BD. Measured by BL-3853 with `Record-CurlExchange.ps1 -Curl wsl.exe`, curl 8.18.0 (OpenSSL, Ubuntu): `curl -s --output-dir $'\xfe' -o a.txt -D $'\xfc.h' URL URL` wrote `\xfe/a.txt` and `\xfc.h`; `curl -s --create-dirs -o $'\xfd/b.txt' URL` made the directory `\xfd`. Route the `-D` open through `RawByteFileName.OpenForWriteAsync` and make a raw-byte directory with the runtime's `SystemNative_MkDir` (libSystem.Native, non-variadic) when the name holds an escaped byte, off Windows only. Check `--skip-existing`'s `Exists` and `--remove-on-error`'s delete as far as curl shows a difference.

## Acceptance criteria

- [ ] `-D $'\xfc.h'` writes a header file with the same name bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [ ] `--create-dirs -o $'\xfd/b.txt'` makes the directory FD and the file in it, as curl 8.18.0 does on Linux.
- [ ] Every other name, and every name on Windows, behaves as before.
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr016HeaderFileAndDirectoryTests.cs` fails before the change and passes after it on every platform.
- [ ] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- To check on Linux without dotnet in WSL: `dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false -p:PublishSingleFile=false -o <dir>` and run `<dir>/curl` from WSL (BL-3853 did this).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1b3b10f1 abe90811 1b81d9c1 5be516e5 3ccadb6a 779790c0.
