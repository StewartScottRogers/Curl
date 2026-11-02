---
id: BL-4543
title: Fix CI failure Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst on Linux and macOS and Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests, Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4543 — Fix CI failure Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst on Linux and macOS and Windows

## Goal

`Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` failed on Linux and macOS and Windows in CI run 41516637939 (https://github.com/StewartScottRogers/Curl/actions/runs/41516637939). First failing commit: 6ada3366.

    CollectionAssert.AreEqual failed. Different number of elements.

Lanes test only on Windows, so reproduce with `gh run view 41516637939 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the harness, not in Curl. `ReferenceRun.EndAsync` cancelled every reference server the moment curl returned, and `InMemoryDuplexStream`'s channel read honours a cancelled token before handing over buffered bytes, so under CI load the MQTT 5 broker lost curl's DISCONNECT (still unread) and saw only CONNECT and PUBLISH ("Different number of elements"). Passed alone here.
- Fix: `EndAsync` now closes curl's end of every TCP connection first (as a real socket closes when curl exits), lets each server drain to end of stream for up to 5 s of real time, and only then cancels. The grace uses `TimeProvider.System` on purpose: the run's own clock may be a fake one a test drives, and the grace is only a bound for a server waiting on something other than curl.
- Focused test: `ReferenceRunTests.EndAsync_ServerHasNotYetReadCurlsLastBytes_DrainsThemBeforeStopping`.
- The fix also let `Frame_Malformed_IsAnsweredWithConnectionClose501` see BL-4541's connection.close 501 every time, so its `Amqp0:AMQP091-4.2.3-4` line came off KnownFailures.txt.
- Measured: Curl.Conformance.Extended.UnitTests 230/230, fast suite green, Measure-CodeQuality for Curl.Conformance.Extended.UnitLibrary 0 failing members. The CI part of the criterion is met by keeping the change platform-neutral (in-memory streams only); the shift's CI watch files a task for any failure.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 106bb302.
- 2026-11-02: Doing -> Done. Reference servers drain curl's last bytes before the harness stops them, so the MQTT 5 CONNECT test sees DISCONNECT every time
