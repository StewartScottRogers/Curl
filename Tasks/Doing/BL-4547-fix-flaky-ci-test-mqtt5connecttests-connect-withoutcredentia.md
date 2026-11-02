---
id: BL-4547
title: Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4547 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes every time in the full fast-test run of `Curl.Conformance.Extended.UnitTests`, not only alone.

## Context

Found by BL-4521 on lane 3 (2026-11-02): it failed once (643 ms) in `dotnet test Curl.slnx --filter "TestCategory!=Integration"` with 230 other tests of the project passing, and passed when run alone. Likely a race under method-level parallelism: the test must wait for the event it reads (the CONNECT read by the reference server) rather than rely on scheduling (lane rules, AF-0226 re-audit). Start at `Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs`.

## Acceptance criteria

- [ ] The test waits for the event it asserts on and holds no state shared with other tests; the cause is named under Notes.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6d1426b8 59888c86.
