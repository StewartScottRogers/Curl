---
id: BL-1649
title: Fix flaky test EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother under full-suite load
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1649 — Fix flaky test EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother under full-suite load

## Goal

`CookieAdversarialTests.EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` passes every time in the full fast run, or the cookie-store race it exposes is fixed.

## Context

- Seen by BL-1647 on 2026-10-07 (lane 9): `dotnet test --filter "TestCategory!=Integration"` over the whole solution failed it once (831 ms, `Curl.Cookies.UnitTests`: 1 failed, 514 passed); rerunning `Curl.Cookies.UnitTests` alone passed it.
- File: `Curl.Cookies.UnitTests/CookieAdversarialTests.cs`. Decide first whether the failure is a real race in `Curl.Cookies.UnitLibrary` (fix it) or a timing assumption in the test (fix the test).
- Reproduce by running the test many times in a loop while the machine is loaded (e.g. the whole fast suite running beside it).

## Acceptance criteria

- [ ] The cause is named under Notes: a race in `Curl.Cookies.UnitLibrary` or a timing assumption in the test.
- [ ] The test passes in 20 consecutive full fast-suite runs (or an equivalent loaded loop recorded under Notes).
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Cookies.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
