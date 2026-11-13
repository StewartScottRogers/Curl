---
id: BL-5097
title: Create --create-dirs directories with mode 0750 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/PhysicalOutputPaths.cs, Curl.Console.UnitTests/PhysicalOutputPathsDirectoryModeTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5097 — Create --create-dirs directories with mode 0750 on Linux and macOS

## Goal

On Linux and macOS, `--create-dirs` creates every directory it makes with mode 0750 (minus the umask), as curl does, instead of 0777 minus the umask.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-052 (Medium, Confirmed twice at 3 umasks). Weaker permissions: directories are world-writable under umask 000 and world-readable under 022. Output files (`-o`, `--output-dir`, `-D` with `--create-dirs`), Linux build.

**Repro (Linux).** With any HTTP server on 127.0.0.1:25080: `umask 022; curl -sS --create-dirs -o cd_t/a/b/f http://127.0.0.1:25080/; stat -c '%n=%a' cd_t cd_t/a cd_t/a/b`. Repeat with `umask 000`, and with `--output-dir cd_t/od -O` and `-D cd_t/hd/h`.

**Expected (curl 8.18.0, OpenSSL build).** `750` for every directory at umask 022, 000 and 027.

**Actual.** `755` at umask 022, `777` at umask 000, `750` at umask 027.

**Where to start.** `Curl.Console/PhysicalOutputPaths.cs`, `TryCreateDirectory` calls `Directory.CreateDirectory(path)` (mode 0777). Off Windows pass a `UnixFileMode` of 0750 (`UserRead|UserWrite|UserExecute|GroupRead|GroupExecute`); the raw-byte path `TryMakeDirectoryUnderRawBytes` needs the same fix. Existing tests: `Curl.Console.UnitTests/PhysicalOutputPathsTests.cs`.

## Acceptance criteria

- [ ] New tests in `Curl.Console.UnitTests/PhysicalOutputPathsDirectoryModeTests.cs`, `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`, create nested directories in a temporary directory through `PhysicalOutputPaths` (both the normal and the raw-byte route) and assert mode 0750 for each directory created; a pre-existing directory is left unchanged.
- [ ] A Windows-neutral test proves the Windows path still creates directories as before.
- [ ] The directory mode honours the umask (the OS applies it; the test sets nothing it cannot restore, or asserts the requested mode passed to the creation call through a stand-in).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1` for any hand-check server, never Python.
- Review acceptance check, for a Linux hand check: `umask 000; curl -sS --create-dirs -o d1/d2/f http://127.0.0.1:25080/; stat -c %a d1 d1/d2` prints `750` twice.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 7ab13906 660c736f 26880e21 a0b49a29.
