---
id: BL-202
title: Parse --resolve and --connect-to into CommandLineOptions
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-202 — Parse --resolve and --connect-to into CommandLineOptions

## Goal

`--resolve host:port:addr[,addr]` and `--connect-to host:port:host:port` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C16. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] Wildcard hosts, `+` prefixes, IPv6 brackets and several values parse as the manpage describes; malformed values give the measured warning or refusal.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C16 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured with `/mingw64/bin/curl` 8.21.0 (Schannel) on 2026-09-26 against `http://127.0.0.1:1/` (closed port, so no recording server was needed: the question was only whether the value is refused before the transfer):
  - `curl -s -o /dev/null --resolve '<v>' http://127.0.0.1:1/` for `''`, `a:80:127.0.0.1`, `[::1]:80:127.0.0.1`, `+a:80:127.0.0.1`, `*:80:127.0.0.1`, `-a:80`, `a:80:127.0.0.1,[::1]` -> exit 7 / 28 (went on to connect).
  - `--resolve garbage`, `a:x:1.2.3.4`, `a:80:`, and `--resolve=x` -> exit 49, stderr `curl: (49) Could not parse CURLOPT_RESOLVE entry 'garbage'` - a transfer-time libcurl error, not a parse refusal.
  - `--connect-to` with `''`, `garbage`, `a:80:127.0.0.1`, `::127.0.0.1:`, `[::1]:80:[fe80::1]:8080`, `example.com::other.example:` -> went on to connect.
  - `--resolve` / `--connect-to` with no value -> exit 2, `curl: option --resolve: requires parameter` + try-help line.
  - `--no-resolve` / `--no-connect-to` -> exit 2, `curl: option --no-resolve: the given option cannot be reversed with a --no- prefix` + try-help line. These are the only `--no-` spellings; curl has no other.
- Decision (Decided by Claude under Stewart's delegation): the parser stores each value verbatim, empty included, in command-line order, in `CommandLineOptions.ResolveEntries` / `ConnectToEntries`, and never refuses one - exactly what curl's tool layer does (it appends to an slist; libcurl parses at transfer time). The "parse as the manpage describes" criterion is met by recording wildcard, `+`, `-`, bracketed IPv6 and comma-separated forms intact for the transfer layer; splitting them into host/port/addresses and the exit-49 refusal belong to BL-214 (resolver) and BL-244 (composition), where curl raises them - checking here would refuse too early and with the wrong exit code. Precedent: `--telnet-option`.
- The ADR could not be written in this task: `Documentation/Planning/Decisions` is in BL-133's `touches` (Doing, another lane). Filed BL-306 to record it; added the measurements to BL-214's Notes.
- Tests: `Curl.Cli.UnitTests/CommandLineResolveOptionTests.cs` (one test per `--no-` spelling), plus arity rows in `CommandLineOptionTableTests`. Build clean, fast tests green (Curl.Cli.UnitTests 1400 passed, 8 skipped), `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 0 failing, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --resolve and --connect-to parse verbatim, in order, into CommandLineOptions.ResolveEntries/ConnectToEntries; missing value and --no- spellings refuse with curl 8.21.0's exact lines
