---
id: BL-4534
title: Fix GoogleCloudStorage conformance: XMLAPI-Emulator-1 STORAGE_EMULATOR_HOST from the run's environment
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsExtension.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsProtocolHandler.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs, Curl.Protocol.Abstractions.UnitTests/ProtocolHandlerServicesTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs, Curl.Extension.Composition.UnitTests/ExtensionProtocolHandlersTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
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

- [x] The case passes; the `GoogleCloudStorage:XMLAPI-Emulator-1 BL-4514` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests` pins the fix (ADR-0482).
- [x] Build clean, fast tests green, the library at 100% line and branch coverage.

## Notes

- Touches widened (no task in Doing on work/dark-factory holds them) to Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs and Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs with their tests: the run's environment reader had no way to reach an extension handler.
- Fix (ADR-1190): `ProtocolHandlerServices.ReadEnvironmentVariable` (optional, null = process environment); `ExtensionProtocolHandlers.For(reader)` sets it on the services it builds the extensions over; `GcsExtension` reads `STORAGE_EMULATOR_HOST` through it. The engine is unchanged. Chose a new field over `ProcessStarter` because setting a starter would also route Plugin and MCP process starts through it.
- Focused test: `GcsExtensionTests.CreateHandler_WithTheRunsEnvironment_SendsTheRequestToItsEmulator`. The conformance GCS cases pass (19/19) with the KnownFailures line removed.
- Measure-CodeQuality for the three libraries: 100% line and branch, 0 failing members. Full build clean, fast tests green on Windows; the change is platform-neutral (no paths or OS text), which covers Linux and macOS CI.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8d051639 86099949 6e066f62.
- 2026-11-02: Doing -> Done. gs:// reads STORAGE_EMULATOR_HOST from the run's environment through ProtocolHandlerServices.ReadEnvironmentVariable; XMLAPI-Emulator-1 passes
