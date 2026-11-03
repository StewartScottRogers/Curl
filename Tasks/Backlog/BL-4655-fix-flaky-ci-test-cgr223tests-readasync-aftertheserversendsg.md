---
id: BL-4655
title: Fix flaky CI test Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests/Cgr223Tests.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4655 — Fix flaky CI test Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText

## Goal

`Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText` passes in every full fast run, not only alone.

## Context

Found by BL-4651 on lane 9 (2026-11-02): it failed once in `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` (Curl.Networking.UnitTests: 1 failed of 4032) and passed when run alone straight after. BL-4651 changed nothing in Curl.Networking. Look in `Curl.Networking.UnitTests/Cgr223Tests.cs` for a read that does not wait for the server side's write, a timing assumption, or state shared between parallel tests.

## Acceptance criteria

- [ ] The test waits for the event it needs instead of relying on scheduling, and passes in the full fast run.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-02: Created.
