---
id: BL-2865
title: Interpret upstream test971.pl and test1486.pl in C# over Curl's option and write-out tables and put cases 971 and 1486 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2863, BL-2864]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest971Script.cs, Curl.Conformance.UnitLibrary/UpstreamTest1486Script.cs, Curl.Conformance.UnitTests/UpstreamTest971ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamTest1486ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2865 — Interpret upstream test971.pl and test1486.pl in C# over Curl's option and write-out tables and put cases 971 and 1486 on the ratchet

## Goal

Upstream cases 971 ("options-in-versions and docs/cmdline-opts are in sync") and 1486 ("write-out.md and tool_writeout.c are in sync") end `Passed` with `$env:CURL_UPSTREAM_CASE` on Windows, through C# interpretations of test971.pl and test1486.pl over the vendored docs and Curl's option table (`CommandLineOptionTable`, `CurlOptionAliasTable`) and write-out variable table, and are on `PassingUpstreamCases.txt`.

## Context

Split from BL-2850. Needs the dispatch seam (BL-2863) and the vendored docs (BL-2864). Read both scripts at tag `curl-8_22_0`. Where the check finds Curl's table differing from upstream's docs, file a task to fix Curl (in `Curl.Cli.UnitLibrary`, with a focused unit test in `Curl.Cli.UnitTests`, ADR-0482, keeping `--ai-help` right) rather than widening this task. Never run Perl.

## Acceptance criteria

- [ ] Cases 971 and 1486 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Each interpretation has its own unit tests (a matching and a differing input).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
