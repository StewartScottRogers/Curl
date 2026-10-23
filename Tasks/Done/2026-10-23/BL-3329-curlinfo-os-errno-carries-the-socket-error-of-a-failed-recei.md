---
id: BL-3329
title: CURLINFO_OS_ERRNO carries the socket error of a failed receive on a kept connection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3304]
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitTests/ConnectionEndPointRecorderTests.cs, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3329 — CURLINFO_OS_ERRNO carries the socket error of a failed receive on a kept connection

## Goal

Curl

## Context

'

## Acceptance criteria

s CURLINFO_OS_ERRNO after a receive that fails on a kept connection is the socket error of that receive (10054 or 10053 on Windows), as libcurl 8.22.0

## Notes

- Fix: `HttpTransferException.OfReceiveFailure` carries the failed receive's `SocketException.NativeErrorCode` as `OsErrno`; `HttpProtocolHandler.WithHeadBeforeFailure` copies it onto the report (`TransferReport.OsErrno`). `ConnectionEndPointRecorder` needed no change (it only overrides OsErrno after a failed connect). Pinned by `ExecuteAsync_ReceiveFailsWithASocketError_ReportsItsOperatingSystemNumberAsOsErrno` (platform-neutral). Fast Http tests green (2258 passed); Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members.
- The task file's Goal, Context and criterion text were truncated at filing; read as the title says. Not reworded.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing.
- 2026-10-23: Doing -> Done. CURLINFO_OS_ERRNO carries the socket error of a failed receive
