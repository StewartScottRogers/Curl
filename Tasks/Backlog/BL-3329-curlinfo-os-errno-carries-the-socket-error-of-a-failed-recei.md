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
completed:
---
# BL-3329 — CURLINFO_OS_ERRNO carries the socket error of a failed receive on a kept connection

## Goal

Curl

## Context

'

## Acceptance criteria

s CURLINFO_OS_ERRNO after a receive that fails on a kept connection is the socket error of that receive (10054 or 10053 on Windows), as libcurl 8.22.0

## Notes

## Log

- 2026-10-23: Created.
