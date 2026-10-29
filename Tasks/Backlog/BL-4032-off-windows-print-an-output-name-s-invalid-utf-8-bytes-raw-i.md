---
id: BL-4032
title: Off Windows, print an output name's invalid UTF-8 bytes raw in %{filename_effective} and the File exists warning, as curl on Linux does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Console/RunningTransferState.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4032 — Off Windows, print an output name's invalid UTF-8 bytes raw in %{filename_effective} and the File exists warning, as curl on Linux does

## Goal

On Linux and macOS, `curl -sS -OJ -w "%{filename_effective}"` against `filename="caf\xE9.txt"` prints the bytes 63 61 66 E9 2E 74 78 74, and the `File exists` warning names the file by those bytes, as curl 8.18.0 OpenSSL on Linux does; the same for `-o $'\xff.txt'`.

## Context

Left over from BL-3858 (ADR-0977). Off Windows an output name holding a byte that is not valid UTF-8 carries it as its `InvalidUtf8ByteEscape` surrogate (U+DC00 + byte, ADR-0920); `RawByteFileName` opens the file under the raw bytes, but write-out and warnings print the surrogate through the normal UTF-8 encoder (U+FFFD bytes). Measure the Linux build through WSL with `Record-CurlExchange.ps1` (`-Curl wsl.exe`) before pinning anything. Start at `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` and the warning text in `Curl.Console/RunningTransferState.cs`; the output writer that encodes write-out text may be the narrower place (check `CurlComposition.cs`'s `InvalidUtf8ByteEscape.Encoding` use).

## Acceptance criteria

- [ ] Off Windows, `%{filename_effective}` for an output name holding an escaped byte writes the raw byte, measured against curl on Linux and recorded in Notes.
- [ ] The `File exists` warning writes the raw byte the same way.
- [ ] Windows output is unchanged; unit tests pin both without touching the real disk.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created by BL-3858's run.
