---
id: BL-2882
title: Fix CI failure test307 on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests, Curl.Conformance.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2882 — Fix CI failure test307 on Linux and macOS

## Goal

`test307` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `test307` failed on Linux and macOS in CI run 39370101714 (https://github.com/StewartScottRogers/Curl/actions/runs/39370101714). First failing commit: c8ae701d.

    Assertion failed.

Lanes test only on Windows, so reproduce with `gh run view 39370101714 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `test307` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: BL-2873 decided to take 307 off `PassingUpstreamCases.txt` (its precheck `test307.pl` wants an `openssl` engine in `curl --engine list`, which neither Curl nor curl on OpenSSL 3 lists, so the case skips on every platform), but its commit b7222390d changed only the task file: the `307` line stayed on the ratchet, and on Linux and macOS the listed case is skipped at its precheck and fails.
- Fix: removed `307` from `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, as BL-2873 decided. No production code changed, so no quality run.
- Measured on Windows, 2026-10-21: `dotnet build` clean; fast tests green (Curl.Conformance.UnitTests 3622 passed, 0 failed). The Linux and macOS CI check is the shift's (BL-987 watch).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 8a3c8414.
