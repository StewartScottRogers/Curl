---
id: BL-3328
title: CURLINFO_OS_ERRNO keeps the socket error of an earlier failed connect attempt when a later address connects
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/ConnectResult.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.OsErrno.cs, Curl.Engine.UnitTests/ConnectionEndPointRecorderTests.cs, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3328 — CURLINFO_OS_ERRNO keeps the socket error of an earlier failed connect attempt when a later address connects

## Goal

Curl

## Context

'

## Acceptance criteria

s CURLINFO_OS_ERRNO after a connect that failed on one address and succeeded on the next is the failed attempt

## Notes

- The Goal, Context and criterion text were truncated when filed; read from the title: curl keeps the failed attempt's OS errno when a later address connects. Fix: AddressFamilyRace returns its last error on success, TcpConnector puts it on ConnectResult.Connected(lastSocketError), EndPointRecordingConnector records it. Fast tests green (Abstractions 928, Networking 3433, Engine 2435); Measure-CodeQuality on the three libraries: 0 failing members.


## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing.
- 2026-10-23: Doing -> Done. CURLINFO_OS_ERRNO keeps the failed earlier address's socket error when a later address connects
