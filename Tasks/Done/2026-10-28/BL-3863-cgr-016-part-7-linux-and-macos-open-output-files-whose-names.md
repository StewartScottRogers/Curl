---
id: BL-3863
title: CGR-016 part 7: Linux and macOS open %output{} files whose names hold invalid UTF-8 under their raw bytes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3853]
touches: [Curl.Console/DiskWriteOutFileOpener.cs, Curl.Console/RawByteFileName.cs, Curl.Console.UnitTests/Cgr016WriteOutFileNameTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3863 — CGR-016 part 7: Linux and macOS open %output{} files whose names hold invalid UTF-8 under their raw bytes

## Goal

On Linux and macOS, `curl -s -o /dev/null -w $'%output{\xff.txt}x' URL` creates a file whose name is the bytes FF 2E 74 78 74 holding `x`, as curl 8.18.0 does (today the name is EF BF BD .txt).

## Context

Found by BL-3852 (CGR-016 part 4). The console hands each invalid argv byte b over as the lone surrogate U+DC00+b (BL-3849, `InvalidUtf8ByteEscape`), and `WriteOutTemplateRenderer` passes the `%output{}` name to `IWriteOutFileOpener.TryOpen` unchanged; `Curl.Console/DiskWriteOutFileOpener.cs` opens it with `FileStream`, which encodes the surrogate as U+FFFD. BL-3853 builds the raw-byte file name opening for `-o` (`Curl.Console/RawByteFileName.cs`); reuse it here. Measure real curl in WSL with `Record-CurlExchange.ps1` first; no Python.

## Acceptance criteria

- [x] `%output{}` with a name holding an escaped invalid byte opens the file under the raw bytes on Linux and macOS; the measured curl 8.18.0 command and result are recorded in Notes.
- [x] Windows behaviour is unchanged.
- [x] A focused unit test in `Curl.Console.UnitTests/Cgr016WriteOutFileNameTests.cs` passes on every platform (pinning each platform's answer with OSCondition where they differ).
- [x] Curl.Console stays within the coverage, complexity and CRAP gates (`Measure-CodeQuality.ps1 -Library Curl.Console`).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1 -Port 18863`, curl 8.18.0 (OpenSSL 3.5.5, Ubuntu): `curl -s -o /dev/null -w $'%output{\xff.txt}x' http://172.26.96.1:18863/` exit 0; `ls | od -An -tx1` gave `ff 2e 74 78 74`, contents `x`.
- Curl after, a linux-x64 self-contained build run in WSL (`dotnet publish Curl.Console -r linux-x64 --self-contained -p:PublishAot=false`): `-w $'%output{\xff.txt}x'` then `-w $'%output{>>\xff.txt}y'` (file:// source), both exit 0; name `ff 2e 74 78 74`, contents `xy`.
- Design: `DiskWriteOutFileOpener` takes `isWindows` and the raw open as constructor seams (its one-argument constructor passes `OperatingSystem.IsWindows()` and `RawByteFileName.OpenUnderRawBytes`). Off Windows a name holding U+DC80..U+DCFF opens through BL-3853's raw `open` shim (truncate, or append for `>>`) with mode 0666, curl's `fopen` mode; every other name, and Windows, keep the shared `FileStream`. No new ADR: this applies ADR-0920's decision to `%output{}`.
- Touches widened by `Curl.Console/RawByteFileName.cs`: `OpenUnderRawBytes` went from private to internal so the opener reuses it; no task in Doing on origin/work/dark-factory names that file.
- Windows unchanged: the opener never takes the raw path there (`TryOpen_EscapedByteOnWindows_OpensThroughFileStream`). The tests choose the platform they play, so all run on every platform without OSCondition. CI on Linux and macOS: the change is platform-neutral and the fast tests are green here.
- Gates: `Measure-CodeQuality.ps1 -Library Curl.Console` 0 failing members; `dotnet build` clean; fast tests 44418 passed, 0 failed across 46 projects.

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 75f3ec35 c7c55a1f 57f19d7e 3ccadb6a 779790c0.
- 2026-10-28: Doing -> Done. Off Windows %output{} opens a name holding invalid UTF-8 bytes under those raw bytes, as curl 8.18.0 does
