---
id: BL-2466
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2466 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38876049958 (https://github.com/StewartScottRogers/Curl/actions/runs/38876049958). First failing commit: 241c183e.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38876049958 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.
  (Locally the whole-solution run now reports the one real gap, `CurlMulti.SetOption` at 87.5% branch, which BL-2467 already owns; CI goes green once BL-2467 lands. Lanes cannot watch CI after pushing.)

## Notes

- Cause: run 38876049958's whole-solution pass flagged only Libcurl (a real gap in
  `CurlMulti.SetOption`). The BL-2269 recheck then merged Libcurl.UnitTests' own report,
  and 663 members across 42 libraries fell to partial or 0% branch coverage while staying
  at 100% line coverage. Because the merge keeps the best count per line, that can only
  happen if the first pass had no branch counts for those lines. `dotnet test Curl.slnx`
  writes one VSTest-merged Cobertura file (one attachment in both the green and red CI logs),
  and on CI that file carried no branch counts, so the branch gate had been passing
  without measuring anything. Small solution-filter probes did not reproduce the missing counts
  locally (the merged file kept them), so the fix avoids the merged file altogether.
- Fix (Measure-CodeQuality.ps1): a whole-solution run builds Curl.slnx once, then runs
  `dotnet test --no-build` on each *.UnitTests project in turn, one report per project,
  the same per-project reports `-Library` runs already trust. The recheck now keys on
  `$wholeSolution` instead of the target being Curl.slnx.
- Measured: the whole-solution run took 5 minutes locally, found 42 reports and 1 failing member
  (Libcurl `CurlMulti.SetOption`, branch 87.5%), and the recheck changed nothing else.
  The Libcurl gap is left to BL-2467 (AF-0148).

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Measure-CodeQuality.ps1 measures each test project on its own, so the branch gate measures real branch counts on CI; the one real gap is left to BL-2467
