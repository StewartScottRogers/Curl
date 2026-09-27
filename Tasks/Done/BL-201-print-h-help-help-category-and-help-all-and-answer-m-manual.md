---
id: BL-201
title: Print -h/--help, --help <category> and --help all, and answer -M/--manual
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-201 — Print -h/--help, --help <category> and --help all, and answer -M/--manual

## Goal

`-h`, `--help <category>`, `--help all` and `-M` print what curl 8.21.0 prints, byte for byte.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Each of `-h`, `--help all`, `--help http` and `--help category` is byte-equal to curl 8.21.0 (measured), generated from the option table where possible.
- [x] `-M` behaves as measured on the reference build (manual text or its refusal).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-27 with `/mingw64/bin/curl` 8.21.0 (x86_64-w64-mingw32, Schannel), output redirected to files (no terminal, so 79 columns): `curl -h`, `curl --help`, `curl --help all|category|bogus|<each of the 25 categories>`, and `COLUMNS=21|40|200 curl -h` / `COLUMNS=40|200 curl --help all`. All exit 0 with nothing on standard error; every line ends CR LF. The outputs, CR LF made LF, are `Curl.Cli.UnitTests/HelpReference/*.txt`, and `CurlHelpTextTests` pins `CurlHelpText.Lines` against each. `curl -h` is 1290 bytes.
- `curl -M > m.out` and `curl --manual`: 299744 bytes, 7849 CR LF lines, SHA-256 `b283726b16afd8394477299ce5780f7fcaf2043bc4d664d736a09ab591349e9f`, identical under `COLUMNS=40`, exit 0. Stored as `Curl.Cli.UnitLibrary/CurlManual.txt` (LF) and pinned by `CurlManualTests`.
- Parsing measured the same day: `-h`/`-M` end parsing like `-V` (`-h all -V` prints all, `-V -h` the version, `-M -h` the manual, `--bogus -h` is refused); `--help` takes the attached value or the next argument whatever it is (`--help http://x` gives the unknown-category page, `-h -v` the `-v` manual section, `--help ""` the usage page); `-vh` asks for help but `-hv`, `-hs`, `-hM`, `-hZZ` are read as nothing (no URL; `-hs <url>` transfers without `-s`); `--no-help` is refused as not reversible; `--no-manual` is accepted; `manual` in a `-K` file is ignored; `help` in a `-K` file prints the usage page and carries on.
- Decision (ADR-0069): the pages are generated from a copy of curl's own `tool_listhelp.c` table (`CurlHelpTable`) by a port of `tool_help.c`'s layout code, because `CommandLineOptionTable` lists only the options Curl parses and curl's help lists all 274. Source fetched from `https://raw.githubusercontent.com/curl/curl/curl-8_21_0/src/`.
- Default taken: the manual is embedded as measured text rather than generated, since it is fixed per curl version and width-independent.
- Default taken: `help` in a `-K` file is ignored for now (filed as BL-375) rather than half-implemented.
- Touches: added `Documentation/Planning/Decisions` for ADR-0069 and its index row; no task in Doing names it. `Curl.Cli.UnitLibrary/README.md` updated.
- Follow-ups filed: BL-374 (`--help <option>` manual sections), BL-375 (`help` in `-K`), BL-376 (write the pages from `Curl.Console`; `Curl.Console` belongs to BL-240 in Doing, so it was not touched here). Until BL-376, `curl.exe -h` prints nothing and exits 0.
- Gates: `dotnet build -warnaserror` clean; fast tests green (Curl.Cli.UnitTests 1929 passed, 9 skipped); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line, 100% branch, 501 members, 0 failing, worst CRAP 10. `dotnet format --verify-no-changes` reports only pre-existing end-of-line issues in other projects (e.g. `Curl.Networking.UnitLibrary/TcpConnector.cs`).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Cli parses -h/--help [subject] and -M/--manual as curl 8.21.0 does and CurlHelpText/CurlManual give its help pages and manual byte for byte
