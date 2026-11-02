---
id: BL-4565
title: Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Extended.UnitTests/Mqtt5/Mqtt5ConnectTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4565 — Fix flaky CI test Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst

## Goal

`Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst` passes every time in the full fast run.

## Context

Seen by BL-4525's lane on 2026-11-02: in `dotnet test Curl.slnx --filter "TestCategory!=Integration"` on Windows, with other lanes building, it failed once (162 ms, the only failure of the run) and passed when run alone. Likely a race in the case's wait for the reference broker or the in-process run; find what it reads before the event it needs has happened (lane rule: wait for the event, never rely on scheduling order).

## Acceptance criteria

- [x] The cause is found and fixed so the case waits for the event it reads.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- Cause: BL-4525's lane claimed its task at 13:24 (71e236489), before BL-4543's harness drain fix (c1925a693, 13:50) and BL-4544's `ReadUntilClosedServer` (44a5f9691, 13:51) landed, and lanes do not rebase mid-run. So its failure was the race those two commits already fixed: the harness cancelled the broker as soon as curl returned, and the broker lost PUBLISH or DISCONNECT. On the current tree the case waits for the event it reads. `ReferenceRun.EndAsync` closes curl's end and waits for every server to finish, and the broker serves without the stop signal until end of stream, so `broker.ReceivedPackets`, `broker.Connect` and `ClientBytes` are read only after the broker has read curl's whole connection. I re-read the rest of the path (`Mqtt5ClientFlow`, `InMemoryDuplexStream`, `WireExchange`, `ReferenceTcpDialer`) and found no other read that comes before its event, and no shared state: each case has its own broker and run.
- No code change needed. Measured: `dotnet build Curl.slnx -warnaserror` clean; `dotnet test Curl.slnx --filter "TestCategory!=Integration"` green (115 assemblies passed, Curl.Conformance.Extended.UnitTests 358/358).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 684158b5 914c30cc.
- 2026-11-02: Doing -> Done. Mqtt5 CONNECT case waits for the broker to read curl's connection to its end (BL-4543, BL-4544); the failure seen came from a checkout older than those fixes; fast tests green
