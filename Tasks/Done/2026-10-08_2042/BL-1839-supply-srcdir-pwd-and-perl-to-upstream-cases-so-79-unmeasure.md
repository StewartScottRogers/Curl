---
id: BL-1839
title: Supply %SRCDIR, %PWD and %PERL to upstream cases so 79 unmeasured behaviour cases are measured
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Gap/Tools/Measure-UpstreamCases.cs]
lane: no
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1839 — Supply %SRCDIR, %PWD and %PERL to upstream cases so 79 unmeasured behaviour cases are measured

## Goal

Upstream cases whose command lines use `%SRCDIR`, `%PWD` or `%PERL` are run and scored as match or gap, not `unmeasured: unknown-variable`.

## Context

- First gap run 2026-10-08_1640 (BL-1747): 79 behaviour cases are unmeasured as unknown-variable. By placeholder: `%SRCDIR` 27, `%NOLISTENPORT` 17, `%PWD` 14, `%SRCDIR`+`%PWD` 7, `%PERL` 6, `%PERL`+`%SRCDIR` 4, `%PROXYPORT` 2.
- `%SRCDIR` is the extracted `tests` folder of the cached release, `%PWD` the case's working folder, and `%PERL` a perl found on PATH (Git for Windows ships one). When no perl is found, the case stays unmeasured with reason `no-perl`.
- `%NOLISTENPORT` and `%PROXYPORT` are out of scope here.
- Interactive only: `Gap/` is an audit path (ADR-0433, BL-1746). Work on the `audit` branch, merge by green PR.

## Acceptance criteria

- [x] A gap run reports the unknown-variable count down from 79 by at least the 58 cases that use only these three placeholders; Notes record before and after.
- [x] Measure-UpstreamCases.cs has a check, run by the gap tools' self-test or a fixture, that substitutes each of the three.
- [x] Merged to master through a green audit or gap pull request.

## Notes

- 2026-10-08: Merged in PR #82 (CI green on all three platforms). `Measure-UpstreamCases.cs` fills `%SRCDIR` and `%PWD` (both upstream's `tests` folder, forward slashes; `%PWD/%LOGDIR` reduced to `%LOGDIR`) and `%PERL` (perl on PATH, else Git for Windows' `usr\bin\perl.exe`; otherwise the case is `no-perl`). Behaviour run against 8.21.0: unknown-variable 79 to 22 (57 moved: 13 match, 2 gap, 42 harness-unsupported for perl commands, precheck, postcheck or setenv); match 561 to 576, gap 160 to 163. The criterion's 58 was a miscount when this task was filed: the 22 left use `%NOLISTENPORT` (17), `%PROXYPORT` (2) or other variables (3), none of the three. A `--self-test` mode checks each substitution (6 PASS).

- 2026-10-08: Merged in PR #82 (CI green on all three platforms). `Measure-UpstreamCases.cs` fills `%SRCDIR` and `%PWD` (both upstream's `tests` folder, forward slashes; `%PWD/%LOGDIR` reduced to `%LOGDIR`) and `%PERL` (perl on PATH, else Git for Windows' `usr\bin\perl.exe`; otherwise the case is `no-perl`). Behaviour run against 8.21.0: unknown-variable 79 to 22 (57 moved: 13 match, 2 gap, 42 harness-unsupported for perl commands, precheck, postcheck or setenv); match 561 to 576, gap 160 to 163. The criterion's 58 was a miscount when this task was filed: the 22 left use `%NOLISTENPORT` (17), `%PROXYPORT` (2) or other variables (3), none of the three. A `--self-test` mode checks each substitution (6 PASS).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Being worked interactively on branch audit-bl-1839; parked so Doing holds nothing orphaned between shifts
- 2026-10-08: Blocked -> Doing.
- 2026-10-08: Doing -> Done. Merged in PR #82 with CI green on all three platforms
