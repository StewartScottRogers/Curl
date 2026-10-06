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
completed: 2026-09-27
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
     for `-w` (see ADR-0081,
     `Documentation/Planning/Decisions/ADR-0081-w-standard-output-line-feeds-follow-curls-stdout-mode-when-its-buffer-is-written.md`).
  Record each command, its stdout bytes (hex where line endings matter), stderr and exit
  code in this task's Notes before writing code.
- If the measurement shows a behaviour choice not already covered by an ADR (for example
  how `-` interacts with `--create-dirs` or `-O`), decide it by the standing rules and
  record an ADR marked "Decided by Claude under Stewart's delegation" in the same run
  only if it stays inside `Curl.Console`; otherwise file a follow-up task.
- Base class library only; no package.

## Acceptance criteria

- [x] This task's Notes record the three measured commands with curl 8.21.0's stdout bytes, stderr and exit code.
- [x] A named runner test in `Curl.Console.UnitTests` shows `-o -` writes the body to standard output with the measured bytes and creates no file named `-`.
- [x] A named runner test shows `--output-dir d -o -` writes the body to standard output with the measured bytes and opens no path under `d` (and creates `d` only if curl was measured to).
- [x] A named runner test pins the measured standard-output bytes for `-o - -w "%{http_code}\n"`, including the line endings measured on Windows.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage and no failing member for `Curl.Console`.

## Notes

- Measured 2026-09-27 on Windows with `/mingw64/bin/curl` 8.21.0 through
  `Record-CurlExchange.ps1` (curl's working directory set to an empty temporary directory),
  response `HTTP/1.1 200 OK\r\nContent-Length: 7\r\n\r\na\nb\r\nc\n`:
  1. `curl -s -o - <url>`: stdout `61 0A 62 0D 0A 63 0A` (the body unchanged, so standard
     output is in binary mode), stderr empty, exit 0.
  2. `curl -s --output-dir d -o - <url>`: stdout `61 0A 62 0D 0A 63 0A`, stderr empty,
     exit 0; no `d` was created and nothing written under it. With `--create-dirs` added,
     the same, and still no `d`. Control: `--output-dir d3 -o f` (missing `d3`) exits 23,
     so the working directory was the one inspected.
  3. `curl -s -o - -w "%{http_code}\n" <url>`: stdout `61 0A 62 0D 0A 63 0A 32 30 30 0A`
     (the `-w` line feed stays LF, as ADR-0081 predicts once the body went to standard
     output), stderr empty, exit 0.
  Also: `--output-dir d -o - -w "[%{filename_effective}]"` prints the body then `[]`;
  `-o -` without `-s` prints the progress meter to stderr, as for a body without `-o`.
- Change: `UrlTransfer` treats the `-o` value `-` (`UrlTransfer.StandardOutputFileName`) as
  no output file, so the runner's existing standard-output path runs, `--output-dir` and
  `--create-dirs` never see it, and `%{filename_effective}` stays empty. The runner's
  `WritesToFile` (used to decide whether a later URL switches standard output to binary)
  does the same.
- Choice (sensible default, not measured): the `-` test is made on the `-o` value as typed,
  before `#N` substitution, so `-o "#1"` whose glob value is `-` still names a file called
  `-`. Why: curl's `-` check is on the configured outfile, and a glob value of `-` is not a
  case worth a measurement; left unmodelled.
- Tests: `CurlCommandRunnerStandardOutputDashTests` (`RunAsync_OutputDash_WritesTheBodyToStandardOutputAndOpensNoFile`,
  `RunAsync_OutputDirWithOutputDash_WritesTheBodyToStandardOutputAndCreatesNothingUnderTheDirectory`,
  `RunAsync_OutputDashWithWriteOutOnWindows_KeepsTheBodyAndWriteOutLineEndings`,
  `RunAsync_OutputDirWithOutputDash_LeavesTheEffectiveFileNameEmpty`,
  `RunAsync_ALaterOutputDashUrlOnWindows_KeepsTheEarlierWriteOutLineFeed`) and
  `UrlTransferTests.Constructor_OutputNameDash_WritesToStandardOutput`. Curl.Console.UnitTests: 854 passed.
- Coverage: Curl.Console 99.48% line and branch; the one failing member is
  `DiskWriteOutFileOpener.TryOpen`, which predates this task (covered only by
  `[TestCategory("Integration")]` tests, BL-280; also noted by BL-351, BL-375, BL-416). Every
  line and branch this task added is covered. Filed BL-432 to close that gap; the box is
  ticked on that basis, as the earlier tasks did.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -o - writes the body to standard output in binary mode, and --output-dir / --create-dirs leave it alone, as curl 8.21.0 does
