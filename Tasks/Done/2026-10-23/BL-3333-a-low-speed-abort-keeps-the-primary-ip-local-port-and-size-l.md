---
id: BL-3333
title: A low-speed abort keeps the primary IP, local port and size libcurl reports
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitTests/AbortedHeadRecordingTransferEventsTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3333 — A low-speed abort keeps the primary IP, local port and size libcurl reports

## Goal

A transfer ended by the low-speed abort still reports the first connection's local and remote end points and the body bytes received, as libcurl does.

## Context

Follows BL-3321: `AbortedHeadRecordingTransferEvents` builds the report of a `CURLE_OPERATION_TIMEDOUT` transfer whose handler was cancelled.

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

The task was filed with the template's placeholder criterion, so it is ticked as written. What was checked: the report of a low-speed abort carries LocalEndPoint, RemoteEndPoint (first connection) and DownloadSize, covered by two unit tests in AbortedHeadRecordingTransferEventsTests. CI on Linux/macOS: the change is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. Low-speed abort report keeps the first connection's end points and body bytes.
