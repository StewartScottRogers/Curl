---
id: BL-4539
title: Fix S3 conformance: AWS_ENDPOINT_URL and AWS_REGION from the run's environment
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Extension.cs, Curl.Extended.Protocol.S3.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
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

- [ ] `S3:Endpoints-1` and `S3:Regions-1` pass in `Curl.Conformance.Extended.UnitTests`.
- [ ] A focused unit test in `Curl.Extended.Protocol.S3.UnitTests` pins that the handler reads the injected environment, not the process's (ADR-0482).
- [ ] Both `KnownFailures.txt` lines naming BL-4514 are removed; the known-failures check passes.
- [ ] `dotnet build` clean and the fast tests green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 0bc13b29 c6122747 585afcef bf6a8285.
