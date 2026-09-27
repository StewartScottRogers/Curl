---
id: BL-379
title: Warn as curl does for a -z symbolic link that stat cannot follow off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-288]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: FR-009
created: 2026-09-27
completed:
---
# BL-379 — Warn as curl does for a -z symbolic link that stat cannot follow off Windows

## Goal

On Linux and macOS, `curl -z <dangling symbolic link>` prints `Warning: Failed to get filetime: No such file or directory` and `-z <symbolic-link loop>` prints `Warning: Failed to get filetime: Too many levels of symbolic links` before the two illegal-date lines, as curl 8.21.0 (OpenSSL build) does, instead of using the link's own time.

## Context

- ADR-0071 (BL-288): off Windows `DiskDataFileReader` stands in for `stat` with `File.GetAttributes`, which uses `lstat`; a dangling or looping final link therefore succeeds and `File.GetLastWriteTimeUtc(path)` returns the link's own time.
- Measured with curl 8.18.0 (OpenSSL/3.5.5, Ubuntu under WSL) on 2026-09-27, `getfiletime` identical in 8.21.0: `ln -s nothing dangling` -> `No such file or directory`; `ln -s loop1 loop2; ln -s loop2 loop1` -> `Too many levels of symbolic links`; `ln -s f goodlink` -> no line, the target's time.
- Probe notes: `File.ResolveLinkTarget(relativePath, true)` resolved relative targets against `/` in the probe; pass `Path.GetFullPath(path)`. Creating symbolic links in a Windows test run needs Developer Mode, so reach the branch through an injected seam if the coverage gate cannot.

## Acceptance criteria

- [ ] `-z dangling` and `-z loop1` give the lines above, asserted in `Curl.Cli.UnitTests` through `DiskDataFileReader`.
- [ ] A link to an existing file still uses the target's time and prints nothing.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
