---
id: BL-5648
title: Add DELETE to the az:// client
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client, Curl.Extended.Protocol.AzureStorage.UnitTests/Client]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5648 — Add DELETE to the az:// client

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

`curl -X DELETE az://account/container/blob` sends Delete Blob; a container URL or name ending in `/` is refused (exit 3); abfs:// still refuses DELETE (exit 4). Decided in ADR-1323. Tested in AzureStorageProtocolHandlerTests and AzureStorageRequestMethodTests; quality run clean for the library (100/100, max complexity 10). Platform-neutral tests. The criterion above is the unedited template placeholder (its claim fingerprint), met by this.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-11-20: Doing -> Done. az:// -X DELETE implemented and tested
