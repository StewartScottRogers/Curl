---
id: BL-4623
title: Fix OciRegistry conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for oci
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client, Curl.Extended.Protocol.OciRegistry.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4623 — Fix OciRegistry conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for oci

## Goal

`curl -v` and `curl --fail-with-body` behave for `oci` as curl's manual says any transfer does, so the BL-4615 cases `CurlSurface:Verbose-Oci-1`, `CurlSurface:FailWithBody-Oci-1` pass.

## Context

BL-4615 ran curl's generic transfer options through `Curl.Console` in process against each storage scheme's reference server (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/`, `OciTransferCases`; shared checks in `StorageTransferChecks`). `-o`, `-O`, `-w`, `--fail` and `-m` pass; two checks fail for every storage scheme:

- **-v** (curl 8.22.0 manual, "-v"): standard error is empty. It must hold the connection line `* Connected to <host> (127.0.0.1) port 443` and the TLS handshake lines (`* SSL connection using ...`), standard output only the object. The HTTP engine's lines come from `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs`; the extension handler's connection path writes none. The same gap holds for all four storage libraries (BL-4620 to BL-4623), so look first for a cause shared through the connection the handler receives; if it is shared, fix it once and close the other three tasks' Verbose lines with it.
- **--fail-with-body** (manual, "--fail-with-body"; ADR-1090, ADR-1093, ADR-1099, ADR-1105: a status of 300 or more is exit 22): exit 22 is right, but standard output is empty; curl writes the error body to standard output before failing. `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs` shows how a handler reads the option.

## Acceptance criteria

- [ ] `CurlSurface:Verbose-Oci-1`, `CurlSurface:FailWithBody-Oci-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins each fix (ADR-0482).
- [ ] `dotnet build` is clean, the fast tests are green and `Curl.Extended.Protocol.OciRegistry.UnitLibrary` stays within every quality gate.

## Notes

## Log

- 2026-11-02: Created.
