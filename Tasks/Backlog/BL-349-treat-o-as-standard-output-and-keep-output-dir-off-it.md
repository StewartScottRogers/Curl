---
id: BL-349
title: Treat -o - as standard output, and keep --output-dir off it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-349 — Treat -o - as standard output, and keep --output-dir off it

## Goal

`-o -` sends the response body to standard output exactly as curl 8.21.0 does, and `--output-dir` never turns `-o -` into a file path.

## Context

- In curl, `-o -` sends the body to standard output (reported while finishing BL-239;
  the online manpage, which documents 8.23.0 as of 2026-09-27, was not found to state it
  in the `-o` excerpt checked, so the measurement below is the authority). Curl.Console
  still treats `-` as an ordinary file name and opens a file called `-`
  (`Curl.Console/CurlCommandRunner.cs`, `CreateOutputFileStream`, and the output-target
  code BL-239 added in `Curl.Console/OutputFileTarget.cs` /
  `Curl.Console/PhysicalOutputPaths.cs`). Since BL-239, `--output-dir d -o -` would open
  `d/-`.
- Measure before pinning, per the standing rule in root `CLAUDE.md` ("Decisions"): use
  the mingw build `/mingw64/bin/curl` (8.21.0) with `Record-CurlExchange.ps1` (repository
  root) against its loopback server, for at least:
  1. `curl -o - <url>`;
  2. `curl --output-dir d -o - <url>` (does `d` get created or touched? is anything
     written under it?);
  3. `curl -o - -w "%{http_code}\n" <url>` on Windows, with a body containing `\n` and a
     `\r\n`, to see whether standard output is in text or binary mode for the body and
     for `-w` (see ADR-0040,
     `Documentation/Planning/Decisions/ADR-0040-w-standard-output-line-feeds-follow-curls-stdout-mode-when-its-buffer-is-written.md`).
  Record each command, its stdout bytes (hex where line endings matter), stderr and exit
  code in this task's Notes before writing code.
- If the measurement shows a behaviour choice not already covered by an ADR (for example
  how `-` interacts with `--create-dirs` or `-O`), decide it by the standing rules and
  record an ADR marked "Decided by Claude under Stewart's delegation" in the same run
  only if it stays inside `Curl.Console`; otherwise file a follow-up task.
- Base class library only; no package.

## Acceptance criteria

- [ ] This task's Notes record the three measured commands with curl 8.21.0's stdout bytes, stderr and exit code.
- [ ] A named runner test in `Curl.Console.UnitTests` shows `-o -` writes the body to standard output with the measured bytes and creates no file named `-`.
- [ ] A named runner test shows `--output-dir d -o -` writes the body to standard output with the measured bytes and opens no path under `d` (and creates `d` only if curl was measured to).
- [ ] A named runner test pins the measured standard-output bytes for `-o - -w "%{http_code}\n"`, including the line endings measured on Windows.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
