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
completed:
---
# BL-3328 — CURLINFO_OS_ERRNO keeps the socket error of an earlier failed connect attempt when a later address connects

## Goal

Curl

## Context

'

## Acceptance criteria

s CURLINFO_OS_ERRNO after a connect that failed on one address and succeeded on the next is the failed attempt

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing.
