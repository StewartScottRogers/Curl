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
completed: 2026-11-02
---
# BL-4655 — Fix flaky CI test Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText

## Goal

`Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText` passes in every full fast run, not only alone.

## Context

Found by BL-4651 on lane 9 (2026-11-02): it failed once in `dotnet test Curl.slnx --no-build --filter "TestCategory!=Integration"` (Curl.Networking.UnitTests: 1 failed of 4032) and passed when run alone straight after. BL-4651 changed nothing in Curl.Networking. Look in `Curl.Networking.UnitTests/Cgr223Tests.cs` for a read that does not wait for the server side's write, a timing assumption, or state shared between parallel tests.

## Acceptance criteria

- [x] The test waits for the event it needs instead of relying on scheduling, and passes in the full fast run.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-11-02, lane 13: Not reproduced in 4 full runs of Curl.Networking.UnitTests. The one scheduling dependence in the test: the server wrote the garbage record as soon as its own handshake returned, which can be before the client has finished its handshake (the server sends the last flight in TLS 1.2), so the record could reach the client inside its last handshake read rather than in ReadAsync. Fix: the server now awaits a TaskCompletionSource the test sets once the client handshake has returned before it writes the record. Test-only change, platform-neutral, so no quality run (no production code changed). `dotnet build Curl.slnx -warnaserror` clean; full fast run green (115 test assemblies, 0 failed).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 9b8f5809 e3a4fd8b.
- 2026-11-02: Doing -> Done. Cgr223 garbage-record test waits for the client handshake before the server writes the record
