---
id: BL-2866
title: Interpret upstream test1707.pl in C# against curl -h <option> and put cases 1707, 1708 and 1710 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2863, BL-2864]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1707Script.cs, Curl.Conformance.UnitTests/UpstreamTest1707ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2866 — Interpret upstream test1707.pl in C# against curl -h <option> and put cases 1707, 1708 and 1710 on the ratchet

## Goal

Upstream cases 1707, 1708 and 1710 ("Verify curl -h --insecure / -h -F / -h --no-clobber") end `Passed` with `$env:CURL_UPSTREAM_CASE` on Windows, through a C# interpretation of test1707.pl that runs the in-process `curl -h <option>` and compares it with the option's text in the vendored `curl.txt`, and are on `PassingUpstreamCases.txt`.

## Context

Split from BL-2850. Needs the dispatch seam with `%CURL` bound (BL-2863) and the vendored docs (BL-2864, which records whether `curl.txt` is vendored or must be built from the `.md` files). Read test1707.pl at tag `curl-8_22_0`. Where Curl's `-h <option>` text differs from real curl 8.22.0's, file a task to fix `CurlHelpTable` in `Curl.Cli.UnitLibrary` (focused unit test in `Curl.Cli.UnitTests`, ADR-0482, `--ai-help` kept right) rather than widening this task. Never run Perl.

## Acceptance criteria

- [ ] Cases 1707, 1708 and 1710 each end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] The interpretation has its own unit tests (a matching and a differing help text).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 73f7d9a9 c5dc9aeb 309149d6.
