---
id: BL-4660
title: Fix OciRegistry conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for oci
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client, Curl.Extended.Protocol.OciRegistry.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4660 — Fix OciRegistry conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for oci

## Goal

`curl -v` and `curl --fail-with-body` behave for `oci` as curl's manual says any transfer does, so the BL-4615 cases `CurlSurface:Verbose-Oci-1`, `CurlSurface:FailWithBody-Oci-1` pass.

## Context

BL-4615 ran curl's generic transfer options through `Curl.Console` in process against each storage scheme's reference server (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/`, `OciTransferCases`; shared checks in `StorageTransferChecks`). `-o`, `-O`, `-w`, `--fail` and `-m` pass; two checks fail for every storage scheme:

- **-v** (curl 8.22.0 manual, "-v"): standard error is empty. It must hold the connection line `* Connected to <host> (127.0.0.1) port 443` and the TLS handshake lines (`* SSL connection using ...`), standard output only the object. The HTTP engine's lines come from `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs`; the extension handler's connection path writes none. The same gap holds for all four storage libraries (BL-4657 to BL-4623), so look first for a cause shared through the connection the handler receives; if it is shared, fix it once and close the other three tasks' Verbose lines with it.
- **--fail-with-body** (manual, "--fail-with-body"; ADR-1090, ADR-1093, ADR-1099, ADR-1105: a status of 300 or more is exit 22): exit 22 is right, but standard output is empty; curl writes the error body to standard output before failing. `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs` shows how a handler reads the option.

## Acceptance criteria

- [x] `CurlSurface:Verbose-Oci-1`, `CurlSurface:FailWithBody-Oci-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins each fix (ADR-0482).
- [x] `dotnet build` is clean, the fast tests are green and `Curl.Extended.Protocol.OciRegistry.UnitLibrary` stays within every quality gate.

## Notes

- -v, two causes. (1) Oci's own: `OciHttpClient` opened every connection with a bare `ConnectTarget`, so the transfer's `Events` never reached the connector and standard error was empty. Fixed in `Client/` without touching `Http/`: the handler wraps the connector in the new `TransferEventsConnector`, which puts the transfer's `Events` and `DiagnosticLog` on every target (registry, token endpoint, redirect), as Redis's `TargetOf` does. (2) Shared by every storage scheme: `StorageTransferChecks.VerboseAsync` looked for `* Connected to <host> (127.0.0.1) port 443`, which curl 8.22.0 no longer writes; BL-4651 measured real curl 8.22.0 (`* Established connection to localhost (127.0.0.1 port 47651) from 127.0.0.1 port 50466`), so the check now expects `* Established connection to <host> (127.0.0.1 port 443) from `. Not measured again: the line comes from curl's connect filter and is the same for every scheme.
- Widened `touches` to `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs`: no task in Doing on origin/work/dark-factory names it.
- With the check corrected, `CurlSurface:Verbose-Az-1`, `-Abfs-1`, `-Abfss-1`, `-Gs-1` and `-S3-1` already pass (those handlers pass the events), so the known-failures check made me drop their KnownFailures lines too, as the Context asks. BL-4657, BL-4658 and BL-4659 are left with only their FailWithBody lines.
- --fail-with-body: `OciTransferException` now carries the error response's body (`ErrorBody`, set by `OciRegistryError.From`), and the handler writes it to the output before failing when `context.Http?.Fail` is `FailWithBody`, as Coap does. Exit codes are unchanged (22, or 67 for a denied login, ADR-1093). No ADR: this follows curl's manual and the existing ADR.
- Unit tests (ADR-0482): `ExecuteAsync_WithTransferEvents_HandsThemToEveryConnectionForVerbosesConnectionLines`, `ExecuteAsync_FailWithBodyMissingManifest_WritesTheErrorBodyAndExits22`, `ExecuteAsync_FailMissingManifest_WritesNothingAndExits22`.
- Checks run: Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.OciRegistry.UnitLibrary found 0 failing members. dotnet build clean. Fast tests: 114 projects passed; after the KnownFailures cleanup Curl.Conformance.Extended.UnitTests passed too (1399 passed, 1 skipped). Platform: the connection line is the same on every platform, and the TLS prefix keeps its per-platform branch. CI on Linux and macOS: the change is platform-neutral and the fast tests are green here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 7eaa623b 5a268f81 c8c37410.
- 2026-11-02: Doing -> Done. curl -v and --fail-with-body for oci:// behave as curl's: connection and TLS lines on stderr, error body on stdout; storage -v check fixed for all schemes
