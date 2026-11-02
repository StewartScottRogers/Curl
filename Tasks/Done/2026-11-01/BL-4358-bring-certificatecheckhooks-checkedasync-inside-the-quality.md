---
id: BL-4358
title: Bring CertificateCheckHooks.CheckedAsync inside the quality gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckHooks.cs, Curl.Extension.Abstractions.UnitTests/Certificates]
requirement: none
created: 2026-10-30
completed: 2026-10-31
---
# BL-4358 — Bring CertificateCheckHooks.CheckedAsync inside the quality gates

## Goal

`CertificateCheckHooks.CheckedAsync` and every method split from it meet the four quality gates: 100% line and branch coverage, cyclomatic complexity at most 10, CRAP at most 30.

## Context

`Curl.Extension.Abstractions.UnitLibrary/Certificates/CertificateCheckHooks.cs`. The quality job measured `CheckedAsync` outside a gate after f34115c66 added connection disposal on a throwing hook.

## Acceptance criteria

- [x] `CertificateCheckHooks.CheckedAsync` is split so no member of `CertificateCheckHooks` exceeds the complexity gate, and `CertificateCheckHooksTests` covers every branch.
- [x] CI's `Quality gates (windows-latest)` job passes on a `work/dark-factory` commit that contains the change; Notes record the run id.

## Notes

- 2026-10-31 (interactive): the lane runs timed out (Log), but the work had already landed: 1ae8bb9f6 "refactor(extension-abstractions): split CertificateCheckHooks.CheckedAsync and cover every branch". CI run 41269170449 on work/dark-factory af52f9656 passed all three `Quality gates shard` jobs and `Quality gates (windows-latest)`, which fails when any member is outside a gate. No code change needed. The template's empty criteria were replaced by these, so the task went Blocked -> Backlog -> Doing to record them.

## Log

- 2026-10-30: Created.
- 2026-10-30: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-30: Doing -> Blocked. Stewart: dark factory timed out after 120 min and again after 30 min of overtime; see Z:\repos\Curl.logs\BL-4358-20261030-060635-L1.jsonl
- 2026-10-31: Blocked -> Backlog. Not a Stewart decision: 1ae8bb9f6 already split CheckedAsync and covered every branch; writing real criteria before closing it
- 2026-10-31: Backlog -> Doing. Criteria at claim: b2dc5e73 be2aa938.
- 2026-10-31: Doing -> Done. 1ae8bb9f6 met the gates; CI run 41269170449 quality jobs green
