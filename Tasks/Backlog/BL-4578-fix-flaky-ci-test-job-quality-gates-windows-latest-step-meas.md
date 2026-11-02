---
id: BL-4578
title: Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4546]
touches: [Measure-CodeQuality.ps1, Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainer.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4578 — Fix flaky CI test job Quality gates (windows-latest) step Measure coverage, complexity and CRAP that failed once on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on every run on Windows, Linux and macOS; it failed once on Windows in the `CI` workflow and passed on the runs either side.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 41537057745 (https://github.com/StewartScottRogers/Curl/actions/runs/41537057745). First failing commit: 040a9c35.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; Amqp1ReferenceContainer.AnswerAsync (async) is outside a gate: branch, complexity; Amqp1ReferenceContainer.Accepts(Curl.Conformance.Extended.Amqp1.Amqp1ReferenceDescribed, Curl.Conformance.Extended.Amqp1.Amqp1ReferenceSymbol[]) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 41537057745 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] What made `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` fail intermittently is named under Notes and removed.
- [ ] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and in the `CI` workflow on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-11-02 (lane 5): The failure is not intermittent and not in `Measure-CodeQuality.ps1`.
  Commit 9598845a7 (BL-4545, part 1 of BL-4527's split) landed
  `Curl.Conformance.Extended.UnitLibrary/Amqp1/Amqp1ReferenceContainer.cs` with `Accepts`
  (complexity 12) and `AnswerAsync` (complexity 12, branch 83.3%) knowingly outside the gates,
  so every quality run that contains that commit fails; the runs "either side" either predate
  it or did not reach the quality job. The run log (41537057745) shows the script working as
  designed: it rechecked `Curl.Conformance.Extended.UnitTests` and still found the two members.
- The fix is BL-4546 ("split Accepts and AnswerAsync"), in Doing on `work/dark-factory` and
  holding `Amqp1ReferenceContainer.cs`. Doing the same split here would collide with that
  lane's edit of the same two methods, so per lane rule 3 this task names the file in
  `touches`, depends on BL-4546 and goes back to Backlog. When BL-4546 is Done, the next claim
  only has to confirm the quality job passes (`Measure-CodeQuality.ps1 -Library
  Curl.Conformance.Extended.UnitLibrary`) and tick the criteria; no script change is needed.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 32ef7b66 e2ff6521.
- 2026-11-02: Doing -> Backlog. Waits on BL-4546 (in Doing), which splits Amqp1ReferenceContainer.Accepts and AnswerAsync, the deterministic cause of the quality failure
