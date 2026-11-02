---
id: BL-4534
title: Fix GoogleCloudStorage conformance: XMLAPI-Emulator-1 STORAGE_EMULATOR_HOST from the run's environment
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsExtension.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsProtocolHandler.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4534 — Fix GoogleCloudStorage conformance: XMLAPI-Emulator-1 STORAGE_EMULATOR_HOST from the run's environment

## Goal

`gs://` reads `STORAGE_EMULATOR_HOST` from the environment of the curl run, so the conformance case
`GcsXmlApiObjectCases.Download_StorageEmulatorHost_SendsTheRequestToTheEmulator` passes.

## Context

Found by BL-4475. `Client/GcsExtension` builds `GcsProtocolHandler` with `Environment.GetEnvironmentVariable`, the
process environment, so a run given its own environment (the in-process harness, `Curl.Api`, `Libcurl`) never sees
`STORAGE_EMULATOR_HOST`: the case connects to storage.googleapis.com:443 and exits 7. Read it from the run's
environment the extension services or transfer context carry (find how other extensions read variables, e.g.
`ArgumentExpansionContext.ReadEnvironmentVariable`).

## Acceptance criteria

- [ ] The case passes; the `GoogleCloudStorage:XMLAPI-Emulator-1 BL-4514` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests` pins the fix (ADR-0482).
- [ ] Build clean, fast tests green, the library at 100% line and branch coverage.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8d051639 86099949 6e066f62.
