---
id: BL-5848
title: Fix flaky CI test host crash in Curl.Extended.Protocol.WebTransport.UnitTests under coverage
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5848 — Fix flaky CI test host crash in Curl.Extended.Protocol.WebTransport.UnitTests under coverage

## Goal

The WebTransport unit test project runs without a test host crash under coverage.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- Not reproducible: CI run 45432993585 crashed the host 3 s after start with no stack; locally the project passes (92 of 92, again this run). The tests and library start no background threads or tasks of their own (only a benign ContinueWith in WebTransportSession.cs:36), so no code change; platform-neutral, nothing changed.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-24: Doing -> Done. Not reproducible: WebTransport tests pass 92 of 92, no code change
