---
id: BL-4539
title: Fix S3 conformance: AWS_ENDPOINT_URL and AWS_REGION from the run's environment
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Extension.cs, Curl.Protocol.Abstractions.UnitLibrary/ProtocolHandlerServices.cs, Curl.Protocol.Abstractions.UnitTests/ProtocolHandlerServicesTests.cs, Curl.Extension.Composition.UnitLibrary/ExtensionProtocolHandlers.cs, Curl.Extension.Composition.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.S3.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4539 — Fix S3 conformance: AWS_ENDPOINT_URL and AWS_REGION from the run's environment

## Goal

The S3 extension reads `AWS_ENDPOINT_URL`, `AWS_REGION` and the other `AWS_*` variables through the run's injected environment reader, so the S3 conformance cases `S3:Endpoints-1` and `S3:Regions-1` pass.

## Context

`Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Extension.cs` `CreateHandler` passes `Environment.GetEnvironmentVariable`
(the process environment) to `S3ProtocolHandler`, so an in-process run (`InProcessCurl.RunAsync` with an environment
reader, as `ExtendedConformanceHarness` passes) cannot set the endpoint or region. Found by BL-4498's cases in
`Curl.Conformance.Extended.UnitTests/S3/S3AddressingCases.cs`. Read the variables through the reader
`ProtocolHandlerServices` carries (or add one there if it has none).

## Acceptance criteria

- [x] `S3:Endpoints-1` and `S3:Regions-1` pass in `Curl.Conformance.Extended.UnitTests`.
- [x] A focused unit test in `Curl.Extended.Protocol.S3.UnitTests` pins that the handler reads the injected environment, not the process's (ADR-0482).
- [x] Both `KnownFailures.txt` lines naming BL-4514 are removed; the known-failures check passes.
- [x] `dotnet build` clean and the fast tests green.

## Notes

- No host gives `ProtocolHandlerServices` a reader: `Curl.Console` passes no `extensionProcessStarter`, so reading `ProcessStarter` alone would not have reached the in-process run. Added an optional `ReadEnvironmentVariable` to `ProtocolHandlerServices`, which `ExtensionProtocolHandlers.For` sets to the reader the host already gives it; S3 reads it, else the process environment (ADR-1192). On rebase the same property and ADR had already landed from BL-4534, so this lane kept the shared ones and dropped its duplicate ADR. Touches widened to `ProtocolHandlerServices.cs` (+ tests), `ExtensionProtocolHandlers.cs` and the Composition `CLAUDE.md`; no task in Doing on `origin/work/dark-factory` named them.
- "Both lines naming BL-4514" read as the two S3 lines (`S3:Endpoints-1`, `S3:Regions-1`); the GoogleCloudStorage and McpClient BL-4514 lines are other tasks' cases (BL-4534 holds GCS) and stay.
- Tests: S3 127, Abstractions 1632 (+2 skipped), Composition 31, Conformance.Extended 164 all pass; full fast run green. Measure-CodeQuality for S3, Abstractions and Composition: 0 failing members (measured with -SkipTestRun over the run's coverage, because the test run hit BL-4538's CoAP `MalformedAcknowledgement_IsSilentlyIgnoredAndTheRequestRetransmitted` failing under load; it passed in the plain fast run and BL-4538 names it).
- CI on Linux and macOS: the change is platform-neutral (no path, OS error or certificate).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 0bc13b29 c6122747 585afcef bf6a8285.
- 2026-11-02: Doing -> Done. S3 reads AWS_ENDPOINT_URL and AWS_REGION from the run's environment reader; S3:Endpoints-1 and S3:Regions-1 pass
