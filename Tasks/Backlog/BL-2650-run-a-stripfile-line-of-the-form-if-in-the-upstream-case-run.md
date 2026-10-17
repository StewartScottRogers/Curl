---
id: BL-2650
title: Run a stripfile line of the form $_ = '' if(...) in the upstream case runner so upstream case 1542 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2540]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2650 — Run a stripfile line of the form $_ = '' if(...) in the upstream case runner so upstream case 1542 runs

## Goal

The upstream case runner applies a `<stripfile>` line of the form `$_ = '' if(<condition>)`, where the condition is `and`/`or` of `$_ =~ /re/` and `$_ !~ /re/` tests, by dropping each line it matches, instead of skipping the case, so upstream case 1542 runs to a verdict.

## Context

Found by BL-2540, which ported `lib1542` (`Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs`). Case 1542 is skipped with `the harness does not run the Perl $_ = '' if(($_ !~ /left intact/) and ($_ !~ /(closing|shutting down) connection #\d+/))`. Once it runs, it may still fail on its own (CURLOPT_MAXLIFETIME_CONN reuse, or the trace's connection lines): if it does, file that as its own task with the differing lines rather than repairing the engine here.

## Acceptance criteria

- [ ] The runner in `Curl.Conformance.UnitLibrary` translates that `$_ = '' if(...)` form, with a focused unit test in `Curl.Conformance.UnitTests` for a matching and a non-matching line.
- [ ] `CURL_UPSTREAM_CASE=1542` reaches a pass or fail verdict, not a skip; a pass is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, a failure filed as a task with its differing lines.
- [ ] Every case on `PassingUpstreamCases.txt` still passes; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
