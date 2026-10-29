---
id: BL-4032
title: Off Windows, print an output name's invalid UTF-8 bytes raw in %{filename_effective} and the File exists warning, as curl on Linux does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Console/RunningTransferState.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/Cgr051RawByteOutputTextTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4032 — Off Windows, print an output name's invalid UTF-8 bytes raw in %{filename_effective} and the File exists warning, as curl on Linux does

## Goal

On Linux and macOS, `curl -sS -OJ -w "%{filename_effective}"` against `filename="caf\xE9.txt"` prints the bytes 63 61 66 E9 2E 74 78 74, and the `File exists` warning names the file by those bytes, as curl 8.18.0 OpenSSL on Linux does; the same for `-o $'\xff.txt'`.

## Context

Left over from BL-3858 (ADR-0977). Off Windows an output name holding a byte that is not valid UTF-8 carries it as its `InvalidUtf8ByteEscape` surrogate (U+DC00 + byte, ADR-0920); `RawByteFileName` opens the file under the raw bytes, but write-out and warnings print the surrogate through the normal UTF-8 encoder (U+FFFD bytes). Measure the Linux build through WSL with `Record-CurlExchange.ps1` (`-Curl wsl.exe`) before pinning anything. Start at `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` and the warning text in `Curl.Console/RunningTransferState.cs`; the output writer that encodes write-out text may be the narrower place (check `CurlComposition.cs`'s `InvalidUtf8ByteEscape.Encoding` use).

## Acceptance criteria

- [x] Off Windows, `%{filename_effective}` for an output name holding an escaped byte writes the raw byte, measured against curl on Linux and recorded in Notes.
- [x] The `File exists` warning writes the raw byte the same way.
- [x] Windows output is unchanged; unit tests pin both without touching the real disk.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured curl 8.18.0 OpenSSL on Linux through WSL on 2026-10-28 (file:// source): `-o $'\xff.txt' -w "%{filename_effective}"` prints FF 2E 74 78 74; with `--no-clobber` and `\xff.txt`, `.1` ... `.99` taken, `--no-progress-meter` gives `Warning: Failed to open the file \xff.txt: File exists` with the raw FF, then `curl: (23) client returned ERROR on write of 3 bytes`.
- The fix landed in neither file the task named: `CurlCommandRunner` picks the `-w` renderer's encoding and the default encoding of its standard error lines. Off Windows both are now `InvalidUtf8ByteEscape.Encoding`, plain UTF-8 except that each escape surrogate is written as its byte (ADR-0978). I added `Curl.Console/CurlCommandRunner.cs` and the new test file to touches because no task in Doing on origin/work/dark-factory names either.
- Windows is unchanged: the warning stays UTF-8 (U+FFFD), and `-w` keeps the ANSI code page path, which already wrote FF for this name. Both are pinned in `Cgr051RawByteOutputTextTests` over an `InMemoryFileSystem`, so no test touches the real disk.
- The retried attempt's open warning (`OutputFileOpenFailureTransferEvents`) and the command-line echo lines still write plain UTF-8 off Windows. They were left alone as outside this task's scope.
- Fast tests are green on Windows. The tests are platform-neutral because `runsOnWindows` is injected. `Measure-CodeQuality.ps1 -Library Curl.Console` reports 0 failing members.

## Log

- 2026-10-28: Created by BL-3858's run.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2e354f2f 00ab634f cf95e9a9 779790c0.
- 2026-10-28: Doing -> Done. Off Windows, %{filename_effective} and the File exists warning print an output name's invalid UTF-8 bytes raw, as curl 8.18.0 on Linux does
