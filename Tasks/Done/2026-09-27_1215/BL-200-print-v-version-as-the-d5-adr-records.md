---
id: BL-200
title: Print -V/--version as the D5 ADR records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-155]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-200 — Print -V/--version as the D5 ADR records

## Goal

`-V`/`--version` prints the lines the BL-155 ADR records for the running OS and exits 0.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-155 ADR has the exact lines per OS and the rule for `Protocols:` and `Features:`.

## Acceptance criteria

- [x] Output is byte-equal to the BL-155 ADR's lines for Windows, Linux and macOS (OS injected in tests).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Added `Curl.Console/CurlCommandRunner.cs` and `Curl.Console.UnitTests` to `touches`: the Cli
  library only records `-V`; the goal (prints the lines and exits 0) needs the runner to write them.
  No task in Doing names either (BL-147 names only `Curl.Console/Curl.Console.csproj`).
- Parsing follows curl 8.21.0 as measured on Windows: `-V` ends parsing where it stands, even in a
  bundle (`-Vo`), so later arguments are neither read nor refused; a refusal before it wins;
  `--no-version` does nothing; a `version` line in a `-K` file is ignored.
- `Protocols:` and `Features:` carry ADR-0021 Decision 6's current state, not the ADR's 2026-09-26
  lines: `http`/`https` (the HTTP handler is registered) and `brotli`/`libz` (it decodes br, gzip
  and deflate). `CurlVersionTextTests` pins the current lines.
- Lines end with `Environment.NewLine` (CRLF on Windows, as the mingw build writes), matching how
  the runner writes standard error.
- `ParseShortBundle` went to complexity 12 with the `-V` check; the flag-letter apply moved to
  `ApplyFlagLetterEndsBundle` to keep it at 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Resumed after a token cut-off; wired the console runner, fixed complexity, verified.
- 2026-09-26: Doing -> Done. curl -V and --version print ADR-0021's four lines for the running OS and exit 0
