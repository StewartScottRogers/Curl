---
id: BL-288
title: Print curl's filetime warning for a -z file lookup that fails off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-246]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-009
created: 2026-09-26
completed:
---
# BL-288 — Print curl's filetime warning for a -z file lookup that fails off Windows

## Goal

On Linux and macOS, `curl -z <file>` whose lookup fails other than as file not found prints
the `Warning: Failed to get filetime: ...` line the OpenSSL build of curl 8.21.0 prints, before
the two illegal-date lines, and `-z ""`, a directory, and the other Windows cases in ADR-0037
match that build too.

## Context

- BL-246 added the fallback (ADR-0037). `DiskDataFileReader` reports a failure reason only when
  `reportsWindowsErrors` is true; off Windows every failure reads as file not found.
- Upstream `getfiletime` off Windows calls `stat` and, for any `errno` but `ENOENT`, prints
  `Failed to get filetime: <strerror(errno)>`. The exact texts (`Not a directory`,
  `Permission denied`, ...) and whether `-z ""` or a directory warns at all must be measured
  on the OpenSSL build of curl 8.21.0 before they are pinned.
- Also left open by BL-246 on Windows: the DOS devices `con` (`CreateFile failed:
  GetLastError 0x00000057`) and `nul` (`GetFileTime failed: GetLastError 0x00000057`),
  measured on curl 8.21.0 on 2026-09-26.

## Acceptance criteria

- [ ] The Linux lines for `-z nodir/x`, `-z <unreadable file>` and `-z ""` are measured on curl
      8.21.0 (OpenSSL build), recorded in `Notes`, and asserted byte for byte in
      `Curl.Cli.UnitTests` through `DiskDataFileReader`'s injected lookup.
- [ ] `-z con` and `-z nul` on Windows give curl's measured line, or `Notes` records why they
      cannot through the base class library.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no
      failing member in `Curl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-26: Created.
