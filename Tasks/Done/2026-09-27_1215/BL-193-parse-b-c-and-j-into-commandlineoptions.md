---
id: BL-193
title: Parse -b, -c and -j into CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-193 — Parse -b, -c and -j into CommandLineOptions

## Goal

`-b`/`--cookie`, `-c`/`--cookie-jar` and `-j`/`--junk-session-cookies` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C7. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl treats a `-b` value containing `=` as a literal cookie string and anything else as a file name (https://curl.se/docs/manpage.html#-b).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `-b 'a=1; b=2'` is a cookie string and `-b jar.txt` is a file name; several `-b` are kept in order.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C7 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Delivered directly rather than through the full `/feature` stages: three table rows plus their `CommandLineOptions` properties, the pattern `Curl.Cli.UnitLibrary/CLAUDE.md` prescribes, needed no architecture plan.
- Measured on curl 8.21.0 (`/mingw64/bin/curl`, Schannel) on 2026-09-26 with `curl <arguments> http://127.0.0.1:1/` (no recording script needed: every case is a parse-time outcome, visible from standard error and the exit code, before any bytes are sent):
  - `-b ''`, `--cookie ''`, `-b nonexist.txt`, `-b a=1`, `-b -x`, `-c -x`, `-j`: accepted with no warning, exit 7 (connection refused).
  - `-c ''` / `--cookie-jar ''`: exit 2, `curl: option -c: blank argument where content is expected` + try-help line.
  - `-b` / `-c` as the last argument: exit 2, `curl: option -b: requires parameter` + try-help line.
  - `--no-cookie`, `--no-cookie=x`, `--no-cookie-jar`, `--no-cookie-jar=x`: exit 2, `curl: option <as typed>: the given option cannot be reversed with a --no- prefix` + try-help line.
  - `--no-junk-session-cookies`, `--no-junk-session-cookies=x`: accepted (exit 7).
- Choice (sensible default): `-b` values go into one ordered `Cookies` list of `CommandLineCookie`, whose `IsCookieString` says whether it holds `=`. curl itself keeps two lists (strings and files); one ordered list keeps the command-line order the criterion asks for and loses nothing, since either split can be derived from it. `-b` is `AcceptingEmpty` (curl reads `''` as a file name); `-c` is `Text`, not `FileName`, because curl gives no looks-like-a-flag warning for it. `-j` is a `NegatableFlag`.
- Tests: `CommandLineCookieOptionTests` (30 cases), plus the three rows in `CommandLineOptionTableTests`. Cli tests 1280 passed; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -b, -c and -j parse into CommandLineOptions.Cookies, CookieJar and JunkSessionCookies with curl 8.21.0's measured refusals
