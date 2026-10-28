---
id: BL-3969
title: CGR-110 part 2: refuse -d @- and the other file readers when stdin is closed (exit 26)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3570, BL-3893, BL-3968]
touches: [Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CommandLineRefusal.cs, Curl.Cli.UnitLibrary/VariableDefinition.cs, Curl.Console/Program.cs, Curl.Cli.UnitTests/Cgr110DataOptionTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3969 — CGR-110 part 2: refuse -d @- and the other file readers when stdin is closed (exit 26)

## Goal

curl -d @- (and --data-binary, --json, --data-urlencode, --url-query, --variable) with a closed stdin exits 26 with 'curl: option <opt>: error encountered when reading a file' and sends nothing, as real curl does.

## Context

Split from BL-3639 (CGR-110, Documentation/Reviews/Curl-Gap-Report-2026-10-25.md: closed stdin with @- is read as empty and the request is sent; real curl 8.22.0 exits 26 for -d/--data-binary/--json and 42 for -F). Read BL-3639's Notes for the measurements and the decided plan.

## Acceptance criteria

- [ ] Program.cs passes ClosedStandardInputStream when Console.OpenStandardInput gives Stream.Null for an invalid handle; tests passing Stream.Null still mean empty.
- [ ] New CommandLineRefusal.StandardInputUnreadable(spelledOption) gives exit 26, no 'Failed to open' line, used by CommandLineOptionTable.ReadAtFile and VariableDefinition.ReadFile; -H @-, --url @- and the other BL-3639 'read as empty' cases still read empty. Unit tests in Cgr110DataOptionTests.cs fail before and pass after (ADR-0482).
- [ ] Real curl and Curl are compared for 'curl -sS -d @- URL <&-' and the --data-binary/--json variants (exit code, stdout, stderr, nothing on the wire); commands and results recorded in Notes.
- [ ] dotnet build is clean and dotnet test --filter "TestCategory!=Integration" passes.

## Notes

- Split from BL-3639.
- Measurements and per-option list are in BL-3639's Notes. Keep --ai-help right if any option text changes.

## Log

- 2026-10-28: Created.
