---
id: BL-2251
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: []
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2251 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 38666857143 (https://github.com/StewartScottRogers/Curl/actions/runs/38666857143). First failing commit: aa14b025.

    [36;1mforeach ($m in @((Get-Content "$env:RUNNER_TEMP\coverage.json" -Raw | ConvertFrom-Json).failing)) {[0m

Lanes test only on Windows, so reproduce with `gh run view 38666857143 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failing member was the `ChooseSignatureScheme` filter lambda in `Curl.Conformance.Http3Server.UnitLibrary/Http3ServerTlsHandshake.cs:161` at 50% branch coverage: no test had the client not offer a server-preferred scheme. Run 38666857143 measured aa14b025, which predates 0ed9339d5 (BL-2247); that commit added `Receive_PreferredSchemeTheClientDoesNotOffer_SignsWithTheNextOne`, which covers the missing branch. No code change was needed here.
- Evidence: CI run 38667881010 at 4c541e2e8 passed Quality gates (windows-latest) and Build and test on Windows, Linux and macOS. Locally, `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http3Server.UnitLibrary` at e0563d248 reports 0 failing members.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. Quality gates green again: BL-2247's test covers the ChooseSignatureScheme branch; CI 38667881010 passed on all three platforms
