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
completed:
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

- [ ] A gap run reports the unknown-variable count down from 79 by at least the 58 cases that use only these three placeholders; Notes record before and after.
- [ ] Measure-UpstreamCases.cs has a check, run by the gap tools' self-test or a fixture, that substitutes each of the three.
- [ ] Merged to master through a green audit or gap pull request.

## Notes

## Log

- 2026-10-08: Created.
