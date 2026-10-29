---
id: BL-3858
title: -J off Windows: write a Content-Disposition filename that is not valid UTF-8 as its raw bytes, as the OpenSSL build does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/ContentDispositionFileName.cs, Curl.Console/RemoteHeaderNameStream.cs, Curl.Console/DeferredOutputFileStream.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3858 — -J off Windows: write a Content-Disposition filename that is not valid UTF-8 as its raw bytes, as the OpenSSL build does

## Goal

On Linux and macOS, `curl -sS -OJ` against a response with `Content-Disposition: attachment; filename="caf\xE9.txt"` creates a file whose name is the bytes `caf\351.txt`, as curl 8.18.0 OpenSSL on Linux does, not `caf\357\277\275.txt` (U+FFFD).

## Context

Left over from BL-3580 (CGR-051, `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`, config-glob-output CGO-08). BL-3580 made `ContentDispositionFileName.Find` read the name in the platform's encoding (the ANSI code page on Windows, UTF-8 elsewhere; ADR-0912), which matches the Windows build and valid UTF-8 names on Linux. Off Windows .NET turns a path string into UTF-8, so a name whose bytes are not valid UTF-8 cannot be created through `System.IO` as those bytes: it needs the name carried as bytes down to the open (for example a libc `open` by P/Invoke on the raw bytes, AOT-compatible, as `NativeExtendedAttributeWriter` and `NativeRegularFileTest` already call libc). Measure the Linux build through WSL with `Record-CurlExchange.ps1` (`-Curl wsl.exe`, BL-474) before pinning anything. Decide how `%{filename_effective}` and the `File exists` warning print such a name (curl writes the raw bytes).

## Acceptance criteria

- [x] Off Windows, `-OJ` with `filename="caf\xE9.txt"` creates the file named by the raw bytes `63 61 66 E9 2E 74 78 74`, measured against curl on Linux, and the measurement is recorded in Notes.
- [x] A valid UTF-8 name still writes the same file as before, and Windows behaviour (BL-3580's `Cgr051Tests`) is unchanged.
- [x] A unit test in `Curl.Console.UnitTests` pins the raw-byte name without touching the real disk.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured (previous claim of this task, 2026-10-28, `Record-CurlExchange.ps1 -Curl wsl.exe`): curl 8.18.0 OpenSSL on Linux, `curl -sS -OJ -w "%{filename_effective}"` against `Content-Disposition: attachment; filename="caf�.txt"`, creates the file named 63 61 66 E9 2E 74 78 74 and prints those raw bytes.
- Decision (ADR-0972): off Windows `ContentDispositionFileName.EncodingFor` gives `InvalidUtf8ByteEscape.Encoding`, so the invalid byte rides as its U+DC00 + byte surrogate and the existing `RawByteFileName` path (already used for `-o` names, via `DeferredOutputFileStream`) opens the file under the raw bytes; no new native call, so `DeferredOutputFileStream.cs` needed no change. Windows keeps the ANSI code page (`Cgr051Tests` unchanged and green).
- `Cgr051RawByteNameTests` pins the raw-byte name, the valid UTF-8 name and the Windows encoding without touching the disk; the tests are pure byte checks, so platform-neutral.
- Printing the raw byte in `%{filename_effective}` and the `File exists` warning (same gap as `-o $'�'`) filed as BL-4027.
- Build clean, fast tests green (Curl.Console.UnitTests 3006 passed), Measure-CodeQuality.ps1 -Library Curl.Console: 0 failing members.

## Log

- 2026-10-26: Created by BL-3580's run for the Linux half of CGR-051.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e2d9f1a6 b2b7d797 d75021b2 779790c0.
- 2026-10-28: Doing -> Done. Off Windows a -J name's invalid UTF-8 bytes open the file under the raw bytes (ADR-0972); write-out printing left to BL-4027.
