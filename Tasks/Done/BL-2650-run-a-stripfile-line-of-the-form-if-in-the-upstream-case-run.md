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
completed: 2026-10-18
---
# BL-2650 — Run a stripfile line of the form $_ = '' if(...) in the upstream case runner so upstream case 1542 runs

## Goal

The upstream case runner applies a `<stripfile>` line of the form `$_ = '' if(<condition>)`, where the condition is `and`/`or` of `$_ =~ /re/` and `$_ !~ /re/` tests, by dropping each line it matches, instead of skipping the case, so upstream case 1542 runs to a verdict.

## Context

Found by BL-2540, which ported `lib1542` (`Curl.Conformance.LibTest.UnitLibrary/Lib1542.cs`). Case 1542 is skipped with `the harness does not run the Perl $_ = '' if(($_ !~ /left intact/) and ($_ !~ /(closing|shutting down) connection #\d+/))`. Once it runs, it may still fail on its own (CURLOPT_MAXLIFETIME_CONN reuse, or the trace's connection lines): if it does, file that as its own task with the differing lines rather than repairing the engine here.

## Acceptance criteria

- [x] The runner in `Curl.Conformance.UnitLibrary` translates that `$_ = '' if(...)` form, with a focused unit test in `Curl.Conformance.UnitTests` for a matching and a non-matching line.
- [x] `CURL_UPSTREAM_CASE=1542` reaches a pass or fail verdict, not a skip; a pass is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, a failure filed as a task with its differing lines.
- [x] Every case on `PassingUpstreamCases.txt` still passes; `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18: The translation already landed with BL-2641 (`UpstreamPerlConditionalEmptying`, reached through `IUpstreamPerlLineEdit.Parse`), which reads `and`-joined `$_ !~`/`$_ =~` terms; test1542's line is that form. No production change was needed. Added `LineEditParse_Test1542Line_KeepsOnlyTheConnectionLines` in `UpstreamPerlConditionalEmptyingTests`: test1542's exact line through the runner's parse, two kept lines (`left intact`, `shutting down connection #0`) and two emptied ones.
- `CURL_UPSTREAM_CASE=1542` now reaches a fail verdict, not a skip: `stderr1542` differs at line 4, where curl reuses connection #0 after `CURLOPT_MAXLIFETIME_CONN` is set to 1 second (expected `shutting down connection #0` then `Connection #1 ... left intact`). Nothing reads `EasyNetworkOptions.MaxConnectionLifetime`. Filed as BL-2730 with the differing lines; 1542 stays off `PassingUpstreamCases.txt`.
- Build clean (0 warnings), fast tests green across the solution.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b3557afb ef0cae9d 03ca90eb.
- 2026-10-18: Doing -> Done. Upstream case 1542 runs to a verdict (fails on MAXLIFETIME_CONN reuse, filed as BL-2730); test1542's strip line pinned by a unit test
