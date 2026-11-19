---
id: BL-5520
title: Fix flaky CI test: one Curl.Networking.UnitTests case fails in the full fast run and passes alone
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-18
completed:
---
# BL-5520 — Fix flaky CI test: one Curl.Networking.UnitTests case fails in the full fast run and passes alone

## Goal

Every Curl.Networking.UnitTests case passes in the full solution fast run on every run, not only when its project runs alone.

## Context

On 2026-11-18 lane 3 (BL-4496, which changed no Networking code) ran `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` at commit c00ea24d19 plus a docs/test-attribute change in Curl.Conformance.Extended.UnitTests: Curl.Networking.UnitTests reported `Failed: 1, Passed: 4364, Skipped: 34`. Running the project alone right after gave `Failed: 0, Passed: 4365`. The failing test's name was not captured. Likely a timing or shared-state race under load (method-level parallel, many test projects at once); a test that waits on real time or on scheduling order rather than the event it needs.

## Acceptance criteria

- [ ] The flaky test is named under Notes (found by running the full fast run, or Curl.Networking.UnitTests repeatedly under load, with the failing name captured).
- [ ] The test waits for the event it needs rather than on scheduling or real time, and passes in the full fast run.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-11-18 (lane 3): Filed from BL-4496's full fast run; see Context.

## Log

- 2026-11-18: Created.
