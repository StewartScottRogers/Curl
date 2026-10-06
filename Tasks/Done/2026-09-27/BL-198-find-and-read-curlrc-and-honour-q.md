---
id: BL-198
title: Find and read .curlrc and honour -q
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-197]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-198 — Find and read .curlrc and honour -q

## Goal

The default config file is found by curl's per-OS search order through an injected file system and environment, read with BL-197's reader, and skipped when `-q`/`--disable` is first.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C12. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Search order involves `CURL_HOME`, `XDG_CONFIG_HOME`, `HOME`, `APPDATA` and the executable's directory (https://curl.se/docs/manpage.html#-K).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] The Windows search order is measured on curl 8.21.0 and pinned; the Linux/macOS order follows the manpage and is tested with a fake environment.
- [x] `-q` as the first argument skips the file; elsewhere it is measured and pinned.
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C12 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Source read: curl 8.21.0 `src/tool_findfile.c` (`findfile`, `conf_list`), `src/tool_parsecfg.c` (`open_config_file`, `parseconfig`), `src/tool_util.c` (`tool_execpath`), `src/tool_operate.c` (`operate`: `.curlrc` is skipped when `argv[1]` starts with `-q` (strncmp 2) or equals `--disable`; its failure is ignored; with no arguments and no URL from it, the try-help line).
- Measured 2026-09-27 with `/mingw64/bin/curl` 8.21.0 in Git Bash. Search order: every directory given a file holding `bogus-<tag>`, variables set with `env -u ... CURL_HOME=<win path> ... curl -V`, curl's error line naming the file read (`curl: <path>:1 config file option 'bogus-<tag>' is unknown`), then that file deleted. Result, pinned in `DefaultConfigFileSearchTests`: `CURL_HOME\.curlrc`, `\_curlrc`, `XDG_CONFIG_HOME\curlrc`, `HOME\.curlrc`, `\_curlrc`, `USERPROFILE`, `APPDATA`, `USERPROFILE\Application Data`, `CURL_HOME/.config\curlrc`, `HOME/.config\curlrc`, then the executable's directory (`.curlrc`, `_curlrc`; measured with a copy of curl.exe). The `dotscore` quirk was measured too: XDG set but empty of the file stops `_curlrc` (HOME\_curlrc passed over) and the `.config` checks; CURL_HOME set stops `HOME/.config`; an empty variable is skipped.
- `-q` measured (`CURL_HOME` holding `bogus-QRC`): `-q -V`, `-qV`, `-qs -V`, `--disable -V`, `-q --no-disable -V` print nothing; `--disable=x -V`, `--no-disable -V`, `-s -q -V`, `-Vq`, `-sq -V` print the two error lines; `curl -q` alone is `curl: (2) no URL specified`. `disable` and `-q` lines in `.curlrc` are ignored (line 3 still refused). A refused `.curlrc` line stops the file, not curl (exit 0 with -V; a later URL line not read); `silent` before it hides the error; a `-K missing.cfg` line prints five lines (the last wrapped). With no arguments and a URL in `.curlrc`, curl transfers.
- Design (decided by Claude under Stewart's delegation, recorded here and in the XML docs rather than an ADR: all behaviour is measured curl, and the one choice is API shape): only the new overload `Parse(..., DefaultConfigFileSearch)` reads `.curlrc`; the existing overloads never do, so the ~1600 existing parser tests and the Console tests stay independent of the machine's home directory. `Curl.Console` is not in this task's touches and already has BL-243 for the wiring; a note was added there. `CommandLineOptions.DefaultConfigFile` carries the path for the `-v` note, which the console prints.
- Not modelled: on Linux, `open()` succeeding on a directory named `.curlrc` (curl then reports `cannot read config`); here a directory reads as unreadable and the search goes on, which is what Windows curl does.
- Gates: `dotnet build Curl.Cli.UnitLibrary -warnaserror` clean; fast tests green (Curl.Cli.UnitTests 1638 passed, 9 skipped by OS); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Lanes cleaned up and cut from 6 to 3; the run had only just resumed and left no work.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Cli finds .curlrc in curl 8.21.0's measured per-OS order through DefaultConfigFileSearch, applies it before the command line, and skips it when -q/--disable is first
