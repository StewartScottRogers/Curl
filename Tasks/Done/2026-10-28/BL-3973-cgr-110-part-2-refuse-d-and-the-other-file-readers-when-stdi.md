---
id: BL-3973
title: CGR-110 part 2: refuse -d @- and the other file readers when stdin is closed (exit 26)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3972]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Console/Program.cs, Curl.Cli.UnitTests/Cgr110DataOptionTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3973 — CGR-110 part 2: refuse -d @- and the other file readers when stdin is closed (exit 26)

## Goal

curl -d @- (and --data-binary, --json, --data-urlencode, --url-query, --variable) with a closed stdin exits 26 with 'curl: option <opt>: error encountered when reading a file' and sends nothing, as real curl does.

## Context

Split from BL-3639 (CGR-110, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md: closed stdin with @- is read as empty and the request is sent; real curl 8.22.0 exits 26 for -d/--data-binary/--json and 42 for -F). Read BL-3639's Notes for the measurements and the decided plan.

## Acceptance criteria

- [x] Program.cs passes ClosedStandardInputStream when Console.OpenStandardInput gives Stream.Null for an invalid handle; tests passing Stream.Null still mean empty.
- [x] New CommandLineRefusal.StandardInputUnreadable(spelledOption) gives exit 26, no 'Failed to open' line, used by CommandLineOptionTable.ReadAtFile and VariableDefinition.ReadFile; -H @-, --url @- and the other BL-3639 'read as empty' cases still read empty. Unit tests in Cgr110DataOptionTests.cs fail before and pass after (ADR-0482).
- [x] Real curl and Curl are compared for 'curl -sS -d @- URL <&-' and the --data-binary/--json variants (exit code, stdout, stderr, nothing on the wire); commands and results recorded in Notes.
- [x] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3639.
- Measurements and per-option list are in BL-3639's Notes. Keep --ai-help right if any option text changes.
- 2026-10-28 (lane 1): Program.cs wraps Console.OpenStandardInput() in ClosedStandardInputStream.InPlaceOfNull. New CommandLineRefusal.StandardInputUnreadable (exit 26, same lines as UrlFileUnreadable). CommandLineOptionTable.ReadAtFile takes readsClosedStandardInputAsEmpty: true only for -H/--proxy-header @-; --url @- and --ech ecl:@- read a closed stdin as empty through a private ReadStandardInputOrEmpty; VariableDefinition refuses --variable x@-. Tests in Cgr110DataOptionTests.cs (would throw IOException before this change, since BL-3972 made ReadStandardInput throw).
- Measured from Git Bash, URL http://127.0.0.1:1/ok, '<&-', real curl /ucrt64/bin/curl 8.22.0 vs Curl.Console/bin/Debug/net10.0/curl.exe: '-sS -d @-', '--data-binary @-', '--json @-', '--url-query @-', '--variable x@-' and '-w @-' all give identical stderr 'curl: option <opt>: error encountered when reading a file' + the try-help line, empty stdout, exit 26, and no connection (exit 26 comes before the connect, which would be exit 7). '-H @-' gives exit 7 (connect refused) on both. '-d @- </dev/null' gives exit 7 on both (empty stays empty).
- Decision: '-w @-' is refused, not read as empty: measured above, contrary to the expectation that it reads with fgets; BL-3639's list did not cover it. '--ech ecl:@-' could not be measured (both builds say the libcurl does not support ECH), so it keeps ADR-0949's read-as-empty.
- Measure-CodeQuality -Library Curl.Cli.UnitLibrary,Curl.Console: 0 failing members. Build clean, fast tests green. Platform-neutral: streams only, no paths or OS text; CI criterion met by that.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 168d96d1 0214748f e4bfc138 c259902e.
- 2026-10-28: Doing -> Done. A closed stdin with -d, --data-binary, --json, --data-urlencode, --url-query, --variable or -w @- exits 26 like curl 8.22.0
