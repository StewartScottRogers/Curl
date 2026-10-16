---
id: BL-2560
title: Curl.Conformance: judge a libtest case's <reply><data> against the program's stdout as runtests.pl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2560 — Curl.Conformance: judge a libtest case's <reply><data> against the program's stdout as runtests.pl does

## Goal

`UpstreamCaseRunner` judges a ported `<tool>` (libtest) case's `<reply><data>` against what runtests.pl 8.22.0 compares it with for a libtest run, so test500, whose `<verify>` has no `<stdout>` and whose `<data>` is not `nocheck`, can pass once lib500 is ported.

## Context

Found by BL-2558: a libtest case run through the ratchet's engine (`UpstreamConformanceTests.CreateEngine`) wrote the body to the program's stdout and matched `<verify><protocol>`, but `UpstreamCaseVerification.CompareReplyData` compared `<reply><data>` against `run.OutputFileBytes`, the `--output` file curl writes and a libtest program never does, so it failed with `the --output file against <reply><data> differs at byte 0 (line 1): expected "HTTP/1.1 200 OK\r\n", got the end`. BL-2558's own test case marks its data `nocheck` to step around it. Read runtests.pl's `singletest_run` and `singletest_check` in curl 8.22.0 (upstream `tests/runtests.pl`, `tests/testutil.pm`) for where a tool run's stdout goes (`%LOGDIR/stdout<N>` or `curl<N>.out`) and which file the `data` comparison reads, and make the runner do the same; record the finding in an ADR if it is a choice.

## Acceptance criteria

- [ ] A test in `Curl.Conformance.UnitTests` runs a lib case whose `<reply><data>` is checked and whose `<verify>` has no `<stdout>`, the program writing the reply's body to stdout, and the case's outcome is what runtests.pl 8.22.0 gives for it.
- [ ] `PassingUpstreamCases.txt` is unchanged or grown with every listed case passing.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 147177d6 f9449db5 2fa0c431 4272a6b9.
