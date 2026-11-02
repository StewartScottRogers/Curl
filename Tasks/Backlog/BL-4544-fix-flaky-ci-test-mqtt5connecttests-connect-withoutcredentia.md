---
id: BL-4544
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
# BL-4544 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes every time in the full fast run.

## Context

Seen by BL-4525's lane on 2026-11-02: in `dotnet test Curl.slnx --filter "TestCategory!=Integration"` on Windows, with other lanes building, it failed once (162 ms, the only failure of the run) and passed when run alone. Likely a race in the case's wait for the reference broker or the in-process run; find what it reads before the event it needs has happened (lane rule: wait for the event, never rely on scheduling order).

## Acceptance criteria

- [ ] The cause is found and fixed so the case waits for the event it reads.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-11-02: Created.
