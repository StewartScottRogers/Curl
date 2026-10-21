---
id: BL-2985
title: List --ai-help in curl --help so AI agents can discover it
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CurlHelpText.cs, Curl.Cli.UnitLibrary/CurlHelpTable.cs, Curl.Cli.UnitLibrary/CurlAiHelpText.cs, Curl.Cli.UnitLibrary/CurlOptionManualSection.cs, Curl.Cli.UnitTests, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitLibrary/UpstreamCaseVerification.cs, Curl.Conformance.UnitLibrary/UpstreamCurlOnlyHelpLines.cs, Curl.Conformance.UnitTests/UpstreamCaseVerificationTests.cs, Curl.Conformance.UnitTests/UpstreamCurlOnlyHelpLinesTests.cs, Documentation/Planning/Decisions/ADR-0224-ai-help-prints-markdown-for-ai-agents-a-short-index-by-default.md]
requirement: none
created: 2026-10-21
completed: 2026-10-21
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

- [x] `curl --help` (no subject) ends with one extra line after curl's own last line
      (`Use "--help [option]" to view documentation for a given option`):
      `Use "--ai-help" for Markdown help written for AI agents (Curl only)` - wording settled and
      recorded in the ADR. Every other line stays byte for byte.
- [x] `curl --help all` and `curl --help curl` list `    --ai-help <category>  Markdown help for AI agents (Curl only)`
      (column-aligned as the table's other rows), in its alphabetical place; `curl --help ai-help`
      prints its documentation as `--help [option]` does for any other option.
- [x] Upstream cases 1461 to 1464 still pass: the harness compares `--help` output with the
      Curl-only `--ai-help` lines removed, and only those lines; a focused test proves an extra
      unexpected line still fails the comparison.
- [x] `--ai-help` stays right: it lists itself or says it is the help being read, and its unit test
      that every option appears still passes.
- [x] ADR-0224 is amended (dated 2026-10-21, quoting Stewart) to say `--help` names `--ai-help`;
      the `--manual` text is unchanged.
- [x] Unit tests in `Curl.Cli.UnitTests` pin the new lines; `dotnet build` is clean, the fast tests
      pass and `Measure-CodeQuality.ps1` passes for `Curl.Cli.UnitLibrary`.

## Notes

- Touches widened (no task in Doing on origin/work/dark-factory named any of these): the stdout
  comparison lives in `UpstreamCaseVerification.cs`, not `UpstreamCaseRunner.cs`, so the filter is
  there, in a new `UpstreamCurlOnlyHelpLines.cs` (Conformance does not reference Curl.Cli, so it
  keeps its own copy of the two lines), with tests in `Curl.Conformance.UnitTests`.
  `CurlAiHelpText.cs` lost its own `--ai-help` row, which would now be listed twice;
  `CurlOptionManualSection.cs` answers `--help --ai-help`.
- `--help ai-help` (no dash) prints the same section as `--help --ai-help`, since the criterion names
  it; curl would print its unknown-category page there.
- The `--ai-help` row's argument is now `<category>` (was `[category]` in `--ai-help`'s own table),
  matching curl's `--help <subject>` style and the criterion's wording.
- Measure-CodeQuality (Curl.Cli and Curl.Conformance): 100% line and branch; one member,
  `CurlHelpText.SubjectText`, was at complexity 12, so the category lookup moved to `CategoryText`.
- Fast tests run: Curl.Cli.UnitTests, Curl.Conformance.UnitTests (test1461 to 1464 pass),
  Curl.Console.UnitTests, Curl.Mcp.UnitTests - all green.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 502cad86 abf01d69 b846daec 0d3ecba0 e0e3e432 478f1298.
- 2026-10-21: Doing -> Done. curl --help, --help all and --help curl name --ai-help; --help --ai-help prints its section; upstream --help cases still pass
