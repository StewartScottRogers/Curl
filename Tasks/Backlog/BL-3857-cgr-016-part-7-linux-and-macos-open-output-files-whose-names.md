---
id: BL-3857
title: CGR-016 part 7: Linux and macOS open %output{} files whose names hold invalid UTF-8 under their raw bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3853]
touches: [Curl.Console/DiskWriteOutFileOpener.cs, Curl.Console.UnitTests/Cgr016WriteOutFileNameTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3857 — CGR-016 part 7: Linux and macOS open %output{} files whose names hold invalid UTF-8 under their raw bytes

## Goal

On Linux and macOS, `curl -s -o /dev/null -w $'%output{\xff.txt}x' URL` creates a file whose name is the bytes FF 2E 74 78 74 holding `x`, as curl 8.18.0 does (today the name is EF BF BD .txt).

## Context

Found by BL-3852 (CGR-016 part 4). The console hands each invalid argv byte b over as the lone surrogate U+DC00+b (BL-3849, `InvalidUtf8ByteEscape`), and `WriteOutTemplateRenderer` passes the `%output{}` name to `IWriteOutFileOpener.TryOpen` unchanged; `Curl.Console/DiskWriteOutFileOpener.cs` opens it with `FileStream`, which encodes the surrogate as U+FFFD. BL-3853 builds the raw-byte file name opening for `-o` (`Curl.Console/RawByteFileName.cs`); reuse it here. Measure real curl in WSL with `Record-CurlExchange.ps1` first; no Python.

## Acceptance criteria

- [ ] `%output{}` with a name holding an escaped invalid byte opens the file under the raw bytes on Linux and macOS; the measured curl 8.18.0 command and result are recorded in Notes.
- [ ] Windows behaviour is unchanged.
- [ ] A focused unit test in `Curl.Console.UnitTests/Cgr016WriteOutFileNameTests.cs` passes on every platform (pinning each platform's answer with OSCondition where they differ).
- [ ] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
