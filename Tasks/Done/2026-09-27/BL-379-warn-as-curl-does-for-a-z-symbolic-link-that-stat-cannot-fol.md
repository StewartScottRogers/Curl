---
id: BL-379
title: Warn as curl does for a -z symbolic link that stat cannot follow off Windows
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-288]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: FR-009
created: 2026-09-27
completed: 2026-09-27
---
# BL-379 — Warn as curl does for a -z symbolic link that stat cannot follow off Windows

## Goal

On Linux and macOS, `curl -z <dangling symbolic link>` prints `Warning: Failed to get filetime: No such file or directory` and `-z <symbolic-link loop>` prints `Warning: Failed to get filetime: Too many levels of symbolic links` before the two illegal-date lines, as curl 8.21.0 (OpenSSL build) does, instead of using the link's own time.

## Context

- ADR-0071 (BL-288): off Windows `DiskDataFileReader` stands in for `stat` with `File.GetAttributes`, which uses `lstat`; a dangling or looping final link therefore succeeds and `File.GetLastWriteTimeUtc(path)` returns the link's own time.
- Measured with curl 8.18.0 (OpenSSL/3.5.5, Ubuntu under WSL) on 2026-09-27, `getfiletime` identical in 8.21.0: `ln -s nothing dangling` -> `No such file or directory`; `ln -s loop1 loop2; ln -s loop2 loop1` -> `Too many levels of symbolic links`; `ln -s f goodlink` -> no line, the target's time.
- Probe notes: `File.ResolveLinkTarget(relativePath, true)` resolved relative targets against `/` in the probe; pass `Path.GetFullPath(path)`. Creating symbolic links in a Windows test run needs Developer Mode, so reach the branch through an injected seam if the coverage gate cannot.

## Acceptance criteria

- [x] `-z dangling` and `-z loop1` give the lines above, asserted in `Curl.Cli.UnitTests` through `DiskDataFileReader`.
- [x] A link to an existing file still uses the target's time and prints nothing.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports no failing member in `Curl.Cli.UnitLibrary`.

## Notes

- Decision recorded in ADR-0090 (Decided by Claude under Stewart's delegation): after
  `File.GetAttributes` succeeds, the off-Windows lookup resolves the final link target with
  `File.ResolveLinkTarget(Path.GetFullPath(path), true)`. A missing target is
  `No such file or directory` (or `Not a directory` when a file stands in its path), a plain
  `IOException` from resolution is the loop (`Too many levels of symbolic links`), and an
  existing target's time is read with `File.GetLastWriteTimeUtc(target)`. Existence is checked
  with `Path.Exists` because `ResolveLinkTarget` returns a `FileInfo` even for a directory.
- Seam: `DiskDataFileReader.ForStatFollowingLinksWith(resolveFinalLinkTarget)`, so the Windows
  coverage run reaches every branch without Developer Mode. Real-symlink tests carry
  `[OSCondition(Linux | OSX | FreeBSD)]` and run in CI; WSL here has no .NET SDK, so they were
  skipped locally.
- The two disk delegates moved into static fields: the compiler's method-group delegate cache at
  a second `new(File.ReadAllBytes, ...)` site showed as half-covered branches.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0090, its README row and
  ADR-0071's follow-up pointer; no task in Doing names it.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -z with a dangling link or a link loop off Windows prints curl's stat warning; a good link uses its target's time
