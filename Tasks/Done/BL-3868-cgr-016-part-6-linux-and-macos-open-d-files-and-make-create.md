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
completed: 2026-10-28
---
# BL-3868 — CGR-016 part 6: Linux and macOS open -D files and make --create-dirs directories whose names hold invalid UTF-8 argument bytes under those raw bytes

## Goal

On Linux and macOS, `curl -D $'\xfc.h' URL` writes the header file named FC 2E 68, and `curl --create-dirs -o $'\xfd/b.txt' URL` makes the directory FD, as curl 8.18.0 does (today EF BF BD in both).

## Context

Split from BL-3853 (CGR-016). BL-3853 (ADR-0920) opens `-o`, `-O` and `-J` files under their raw bytes through `RawByteFileName.OpenForWriteAsync`, used by `DeferredOutputFileStream`. The `-D` file is opened by `CurlCommandRunner` straight through `IFileSystem.OpenForWriteAsync`, and `--create-dirs` makes directories through `PhysicalOutputPaths.TryCreateDirectory` (`Directory.CreateDirectory`), both of which encode a lone surrogate U+DC80..U+DCFF as EF BF BD. Measured by BL-3853 with `Record-CurlExchange.ps1 -Curl wsl.exe`, curl 8.18.0 (OpenSSL, Ubuntu): `curl -s --output-dir $'\xfe' -o a.txt -D $'\xfc.h' URL URL` wrote `\xfe/a.txt` and `\xfc.h`; `curl -s --create-dirs -o $'\xfd/b.txt' URL` made the directory `\xfd`. Route the `-D` open through `RawByteFileName.OpenForWriteAsync` and make a raw-byte directory with the runtime's `SystemNative_MkDir` (libSystem.Native, non-variadic) when the name holds an escaped byte, off Windows only. Check `--skip-existing`'s `Exists` and `--remove-on-error`'s delete as far as curl shows a difference.

## Acceptance criteria

- [x] `-D $'\xfc.h'` writes a header file with the same name bytes as curl 8.18.0 on Linux; the measured command and both results are recorded in Notes.
- [x] `--create-dirs -o $'\xfd/b.txt'` makes the directory FD and the file in it, as curl 8.18.0 does on Linux.
- [x] Every other name, and every name on Windows, behaves as before.
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr016HeaderFileAndDirectoryTests.cs` fails before the change and passes after it on every platform.
- [x] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Plan (feature pipeline run inline; the change is three small edits in files the task names): `CurlCommandRunner.TransferWithHeaderFileAsync` opens `-D` through `RawByteFileName.OpenForWriteAsync`; `PhysicalOutputPaths` takes an optional raw directory maker (`RawByteFileName.MakeDirectoryUnderRawBytes`, `SystemNative_MkDir`, off Windows only) used for a name holding an escaped byte; EEXIST counts as made. Decision recorded in ADR-0942.
- Mode choice: 0777 before the umask, the mode `Directory.CreateDirectory` gives every other name, so raw and valid names get the same permissions (default kept rather than introducing a difference between the two kinds of name).
- Measured 2026-10-28 in WSL against curl 8.18.0 (OpenSSL 3.5.5, Ubuntu) and a linux-x64 self-contained publish of this branch, each in an empty directory, file listing through `find . | od -c`:
  - `curl -s -D $'\xfc.h' -o a.txt file:///etc/hostname`: curl 8.18.0 exit 0, files `./a.txt` and `./\374.h` (FC 2E 68); Curl exit 0, the same two names.
  - `curl -s --create-dirs -o $'\xfd/b.txt' file:///etc/hostname`: curl 8.18.0 exit 0, `./\375` and `./\375/b.txt`; Curl exit 0, the same.
  - Before the change Curl wrote EF BF BD in both (BL-3853's measurement in the Context).
- `--skip-existing`'s `Exists` and `--remove-on-error`'s delete were not measured in this run; filed as BL-3945.
- Unit tests `Cgr016HeaderFileAndDirectoryTests` inject a fake maker, so they run on every platform; they do not compile before the change (no maker parameter). The `-D` route reuses `RawByteFileName.OpenForWriteAsync`, already pinned by `Cgr016OutputFileTests`.
- CI on Linux and macOS: the tests are platform-neutral (fake maker; real-disk tests use valid names or the null-maker path) and the fast tests are green here.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Console` 100% line, 100% branch, 0 failing members, worst CRAP 10 (the platform choice of maker sits in an ADR-0083 excluded method).

- To check on Linux without dotnet in WSL: `dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false -p:PublishSingleFile=false -o <dir>` and run `<dir>/curl` from WSL (BL-3853 did this).

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1b3b10f1 abe90811 1b81d9c1 5be516e5 3ccadb6a 779790c0.
- 2026-10-28: Doing -> Done. Off Windows -D files and --create-dirs directories whose names hold invalid UTF-8 argument bytes are made under those raw bytes, as curl 8.18.0 does
