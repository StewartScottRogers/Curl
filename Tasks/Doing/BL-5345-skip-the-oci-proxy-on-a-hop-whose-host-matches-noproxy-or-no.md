---
id: BL-5345
title: Skip the oci:// proxy on a hop whose host matches --noproxy or NO_PROXY
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/TransferEventsConnector.cs, Curl.Extended.Protocol.OciRegistry.UnitTests/Client/OciRegistryProxyTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5345 — Skip the oci:// proxy on a hop whose host matches --noproxy or NO_PROXY

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: fbecace0.
