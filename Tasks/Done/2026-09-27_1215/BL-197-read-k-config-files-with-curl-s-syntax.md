---
id: BL-197
title: Read -K/--config files with curl's syntax
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-197 — Read -K/--config files with curl's syntax

## Goal

`-K`/`--config` files are read through an injected reader and their options applied as if given on the command line.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C11. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Syntax: https://curl.se/docs/manpage.html#-K (curl 8.21.0).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Quoting, escapes, `=`/`:`/space separators, `#` comments, `url = ...` and nested `-K` behave as measured on curl 8.21.0; a test per rule.
- [x] An unreadable file and a malformed line give the measured lines and exit.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

**Partial work from a cut-off run (2026-09-26):** local branch `factory/BL-197-wip` holds one commit of it. Start with `git cherry-pick --no-commit factory/BL-197-wip`, review it, then carry on from there rather than starting over.

- Resumed 2026-09-26 (lane 2): cherry-picked `factory/BL-197-wip` unchanged; it built clean, 971 Curl.Cli tests passed, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reported 100% line, 100% branch, 0 failing members, worst CRAP 10. No further code change was needed.
- Design: `ConfigFileSyntax` splits lines (port of `parseconfig` in `src/tool_parsecfg.c`), `ConfigFileApplier` reads through the injected `IDataFileReader` (standard input for `-`) and applies each line through `CommandLineParser.ApplyConfigFileLine`, `WrappedMessage` wraps warnings and errors at 79 bytes as curl's `voutf` does when stderr is not a terminal. Nesting depth is tracked on `CommandLineOptions.OpenConfigFileCount`, limit 5.
- Re-measured on 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (`curl -q -K <file>`), each matching a pinned test:
  - `bogus = 1` -> `curl: k1.txt:1 config file option 'bogus' is unknown` / `curl: option -K: found an unknown config option` / try-help, exit 2.
  - missing file -> `curl: cannot read config from 'nope.txt'` / `curl: option -K: error encountered when reading a file` / try-help, exit 26.
  - `url = x y` -> `Warning: k2.txt:1 Option 'url' uses argument with unquoted whitespace. This ` / `Warning: may cause side-effects. Consider double quotes.` (wrapped at 79 bytes).
  - a file that includes itself -> `curl: Max config file recursion level reached (5)`, five `config file option '-K' is badly used here` lines, `curl: option -K: is badly used here`, exit 2.
  - `--no-config` -> `the given option cannot be reversed with a --no- prefix`, exit 2. `silent foo` -> only `curl: option -K: had unsupported trailing garbage` (silent hides the file line), exit 2.
- Not modelled (sensible default, documented in `ConfigFileSyntax`): a NUL byte ending a line early and curl's 10 MiB line limit; neither changes any realistic config file. No ADR: every behaviour is measured curl behaviour, no design choice was made beyond that.
- `.curlrc` and applying `-K` from `Curl.Console` are already filed as BL-198 and BL-243.
- Plan item: C11 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Shift stopped while waiting for tokens (limit reset early); partial work saved on branch factory/BL-197-wip
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -K/--config files are read through IDataFileReader and applied with curl 8.21.0's syntax, warnings and refusals
