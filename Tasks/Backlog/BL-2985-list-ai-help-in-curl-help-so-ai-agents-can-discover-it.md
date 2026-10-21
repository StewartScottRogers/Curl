---
id: BL-2985
title: List --ai-help in curl --help so AI agents can discover it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CurlHelpText.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitTests, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Documentation/Planning/Decisions/ADR-0224-ai-help-prints-markdown-for-ai-agents-a-short-index-by-default.md]
requirement: none
created: 2026-10-21
completed:
---
# BL-2985 — List --ai-help in curl --help so AI agents can discover it

## Goal

`curl --help` and `curl --help all` mention `--ai-help`, so an AI agent that runs the usual
`curl --help` finds the Markdown help without knowing it exists.

## Context

Stewart, 2026-10-21: "Lets make sure that Curl --help also list the option --aihelp even though it is
not part of the original curl implementation. I need the aihelp to be discoverable." The option is
`--ai-help` (BL-911, ADR-0224). ADR-0224 kept `--help` and `--manual` byte for byte with curl's, so
today nothing in `--help` names it; `curl --help all` lists every option but `--ai-help`.

This is a deliberate, additive departure from curl's `--help` bytes, at Stewart's request. Upstream
cases 1461 to 1464 (all in `PassingUpstreamCases.txt`) check `--help` output; they must keep passing.

## Acceptance criteria

- [ ] `curl --help` (no subject) ends with one extra line after curl's own last line
      (`Use "--help [option]" to view documentation for a given option`):
      `Use "--ai-help" for Markdown help written for AI agents (Curl only)` - wording settled and
      recorded in the ADR. Every other line stays byte for byte.
- [ ] `curl --help all` and `curl --help curl` list `    --ai-help <category>  Markdown help for AI agents (Curl only)`
      (column-aligned as the table's other rows), in its alphabetical place; `curl --help ai-help`
      prints its documentation as `--help [option]` does for any other option.
- [ ] Upstream cases 1461 to 1464 still pass: the harness compares `--help` output with the
      Curl-only `--ai-help` lines removed, and only those lines; a focused test proves an extra
      unexpected line still fails the comparison.
- [ ] `--ai-help` stays right: it lists itself or says it is the help being read, and its unit test
      that every option appears still passes.
- [ ] ADR-0224 is amended (dated 2026-10-21, quoting Stewart) to say `--help` names `--ai-help`;
      the `--manual` text is unchanged.
- [ ] Unit tests in `Curl.Cli.UnitTests` pin the new lines; `dotnet build` is clean, the fast tests
      pass and `Measure-CodeQuality.ps1` passes for `Curl.Cli.UnitLibrary`.

## Notes

## Log

- 2026-10-21: Created.
