---
id: BL-239
title: Name output files for -O, -J, --output-dir and --create-dirs in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-194, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-239 — Name output files for -O, -J, --output-dir and --create-dirs in Curl.Console

## Goal

`-O`, `--remote-name-all`, `-J`, `--output-dir` and `--create-dirs` name and create output files as curl does.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W10. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-194 parses these into `CommandLineOptions.UrlOutputs` (one `UrlOutput` per URL: `FileName`, `UsesRemoteName`), `RemoteHeaderName`, `OutputDirectory` and `CreateDirectories`; the pairing rules are in ADR-0029. Read `UrlOutputs[index]` instead of `OutputFiles[index]`. Measured for BL-194: `curl -O http://127.0.0.1:1/` prints `Warning: No remote filename, uses "curl_response"` before the connect error.
- Windows name sanitising exists (`WindowsOutputFileNameSanitizer`, BL-091).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Remote names from the URL path, Content-Disposition parsing under `-J`, and the refusals (no file name in URL, existing file under `-J`) match curl 8.21.0 (measured).
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W10 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-27 with `/mingw64/bin/curl` 8.21.0 (Schannel) through `Record-CurlExchange.ps1 -Port 18239`, run from an empty working directory, body `hello` (`Content-Length: 5`) unless said. `curl <args> http://127.0.0.1:18239/<path>`:
  - `-sS -O /dir/file.txt?q=1#f` -> `file.txt`; `/dir/` -> `dir`; `/a/b//` -> `b`; `/a%20b%3F.txt` -> `a%20b%3F.txt`; `-O file:///C:/Windows/win.ini` -> `win.ini`.
  - `-O /` and `-O http://127.0.0.1:18239` -> `curl_response`, stderr `Warning: No remote filename, uses "curl_response"` (not under `-sS`). `-D nodir/h -O /` prints only the `-D` failure: the name is worked out after `-D` is opened.
  - Windows remote names: `a:b.txt` -> `a_b.txt`, `a*b.txt` -> `a_b.txt`, `con` -> `_con`, `COM1` -> `_COM1`, `clock$` -> `_clock$`, `con.txt` -> `con_txt`, `lpt9.txt` -> `lpt9_txt`, `aux.x.y` -> `aux_x.y`; `a.con`, `nul%20`, `CON%3Ax` kept. `conin$` exits 23 `curl: curl: (23) Failed writing body` (curl opened the console device); not modelled, CONIN$/CONOUT$ are not renamed here either.
  - `-OJ` with `Content-Disposition:` `attachment; filename="x y.txt"` -> `x y.txt`; `filename=plain.txt; size=5` -> `plain.txt`; `attachment;filename=semi.txt;` -> `semi.txt`; `filename='sq.txt'` -> `sq.txt`; `filename="../../dir/evil.txt"` -> `evil.txt`; `filename="C:\x\win.txt"` -> `win.txt`; `filename="a*b?.txt"` -> `a_b_.txt`. URL name `u.txt` instead for: no header, `inline`, `filename*=UTF-8''star.txt`, `FileName=Up.txt`, `filename="dir/"`, a 404 response. A 302 response's name is used (`r.txt`).
  - `-OJ` with `filename=""`: exit 23, stderr `Warning: Failed to open the file : No such file or directory` then `curl: (23) client returned ERROR on write of 46 bytes` (46 = the header line with CR LF).
  - `-OJ` with `x.txt` existing: exit 23, `Warning: Failed to open the file x.txt: File exists`, `curl: (23) client returned ERROR on write of 51 bytes`, `x.txt` unchanged. `u.txt` existing and no header: overwritten. `-sS -C 3 -OJ` with `u.txt` = `abc`: exit 23, `curl: (23) client returned ERROR on write of 51 bytes`, `u.txt` still `abc`.
  - `-J` alone writes to stdout; `-J -o o.txt` writes `o.txt`; `-i -OJ` and `-I -OJ` write every header line (status line included) into `x.txt`.
  - `--output-dir od -OJ` -> `od/x.txt`; `--output-dir od -o sub.txt` -> `od/sub.txt`; `--output-dir od/ -O` -> `od/f.txt`; `-O --output-dir nod` (missing) -> exit 23, `Warning: Failed to open the file nod/f.txt: No such file or directory`, `curl: (23) client returned ERROR on write of 5 bytes`; `--output-dir "o?d" -o x` fails on `o?d/x` (directory not sanitized); `-w %{filename_effective}` prints `od/f.txt`.
  - `--create-dirs -o a/b/c.txt` creates `a`, `a/b`; `-o x\y\z.txt` creates `x`, `x\y`; `-O --output-dir n1/n2 --create-dirs` creates both; `--create-dirs -O` alone creates nothing. With a file `blk`: `--create-dirs -o blk/b/c.txt` exits 23 with `curl: Error creating directory blk/b` then `curl: (23) Failed writing received data to disk/application`, no request sent (0 request bytes); `-sS` keeps both lines, `-s` prints nothing; a second URL is not transferred.
  - `-OJ --output-dir od` with `filename=""` (after review): exit 23, `Warning: Failed to open the file od/: Permission denied`, write of 46 bytes. So only an existing *file* is refused with `File exists`; a directory fails in the open. `IOutputPaths.FileExists` checks files only for this reason.
- Design: `RemoteFileName`, `ContentDispositionFileName`, `WindowsOutputFileNameSanitizer.SanitizeRemoteName`, `OutputFileDirectories`, `RemoteHeaderNameStream` and an `IOutputPaths` seam (`PhysicalOutputPaths`) in Curl.Console; the runner reads `UrlOutputs[index]` and resolves the file in `ResolveOutputFileAsync` after the `-D` open and proxy choice, before the transfer. `TransferContextFactory.Create` takes a `watchHeaderOutput` wrapper so the `-J` stream reads each header line before `-i`/`-D` get it.
- Defaults taken (no ADR: they are implementation choices, not behaviour choices, and `Documentation/Planning/Decisions` is in BL-303's `touches`):
  - "Name already taken" is checked with `IOutputPaths.Exists` before the open, where curl opens with `O_EXCL`: `IFileSystem` (Curl.Protocol.Abstractions, outside this task's `touches`) has no exclusive open. The race window is accepted.
  - The `Content-Disposition` name's bytes are decoded as UTF-8; curl uses them raw.
  - The "No remote filename" warning is printed after the per-transfer warning lines and proxy choice; their relative order to it was not measurable with the loopback server.
  - `%{filename_effective}` is BL-305's; it should print the name this task resolves (`DeferredOutputFileStream.Path` after any `-J` rename; `od/f.txt` measured above).
  - When the `-J` open fails, none of that response's header lines reach `-D`: the whole head is read before it is passed on. curl writes the `-D` lines up to the `Content-Disposition` line first. Not modelled.
  - `PhysicalOutputPathsTests` touch a temporary directory without `TestCategory("Integration")`, as `PhysicalFileSystemTests` do, so the fast run covers `PhysicalOutputPaths`.
- Review (code-reviewer): no must-fix. Applied: `FirstUncreatable` renamed `CreateLeadingDirectories`; `CannotOpenForResumeMessage` renamed `WriteReceivedDataFailedMessage` (it now serves resume, `-D` and `--create-dirs`); cancellation token passed through the `-J` open; array `WriteAsync` override on `RemoteHeaderNameStream`; `CurlComposition` passes `PhysicalOutputPaths`; `DeferredOutputFileStream` docs. Follow-ups filed: BL-348 (exclusive create for `-J`), BL-349 (`-o -` as standard output, and `--output-dir`). UNC paths under `--create-dirs` were not measured.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Console.UnitTests 531 passed, every other project unchanged and green); `dotnet format --verify-no-changes` clean for every file this task touched (`Curl.Console/TransferProxySelection.cs` has LF endings from before this task); `Measure-CodeQuality.ps1 -Library Curl.Console` 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -O, --remote-name-all, -J, --output-dir and --create-dirs name and create output files as curl 8.21.0 does (measured, pinned in Curl.Console tests)
