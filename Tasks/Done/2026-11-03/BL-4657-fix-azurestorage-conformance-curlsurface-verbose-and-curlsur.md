---
id: BL-4657
title: Fix AzureStorage conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for az, abfs, abfss
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client, Curl.Extended.Protocol.AzureStorage.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4657 — Fix AzureStorage conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for az, abfs, abfss

## Goal

`curl -v` and `curl --fail-with-body` behave for `az`, `abfs` and `abfss` as curl's manual says any transfer does, so the BL-4615 cases `CurlSurface:Verbose-Az-1`, `CurlSurface:FailWithBody-Az-1`, `CurlSurface:Verbose-Abfs-1`, `CurlSurface:FailWithBody-Abfs-1`, `CurlSurface:Verbose-Abfss-1`, `CurlSurface:FailWithBody-Abfss-1` pass.

## Context

BL-4615 ran curl's generic transfer options through `Curl.Console` in process against each storage scheme's reference server (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/`, `AzTransferCases`, `AbfsTransferCases` and `AbfssTransferCases`; shared checks in `StorageTransferChecks`). `-o`, `-O`, `-w`, `--fail` and `-m` pass; two checks fail for every storage scheme:

- **-v** (curl 8.22.0 manual, "-v"): standard error is empty. It must hold the connection line `* Connected to <host> (127.0.0.1) port 443` and the TLS handshake lines (`* SSL connection using ...`), standard output only the object. The HTTP engine's lines come from `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs`; the extension handler's connection path writes none. The same gap holds for all four storage libraries (BL-4620 to BL-4660), so look first for a cause shared through the connection the handler receives; if it is shared, fix it once and close the other three tasks' Verbose lines with it.
- **--fail-with-body** (manual, "--fail-with-body"; ADR-1090, ADR-1093, ADR-1099, ADR-1105: a status of 300 or more is exit 22): exit 22 is right, but standard output is empty; curl writes the error body to standard output before failing. `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs` shows how a handler reads the option.

## Acceptance criteria

- [x] `CurlSurface:Verbose-Az-1`, `CurlSurface:FailWithBody-Az-1`, `CurlSurface:Verbose-Abfs-1`, `CurlSurface:FailWithBody-Abfs-1`, `CurlSurface:Verbose-Abfss-1`, `CurlSurface:FailWithBody-Abfss-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins each fix (ADR-0482).
- [x] `dotnet build` is clean, the fast tests are green and `Curl.Extended.Protocol.AzureStorage.UnitLibrary` stays within every quality gate.

## Notes
- Verbose cause: the case, not Curl (the same cause BL-4651 found for Redis). The handler already gives the transfer's `Events` to the connector, which writes `* Established connection to <host> (127.0.0.1 port 443) from ...` and the `* schannel: ` lines; the check looked for `* Connected to <host> (127.0.0.1) port 443`, which curl 8.22.0 no longer writes (BL-4651 measured real curl 8.22.0 with `Record-CurlExchange.ps1`). `StorageTransferChecks.VerboseAsync` now expects the Established prefix up to `from ` (the local port varies) and prints standard error when it fails. Widened `touches` to `StorageTransferChecks.cs`: no task in Doing on origin/work/dark-factory names it. The fix is shared, so `CurlSurface:Verbose-Gs-1` (BL-4658) and `CurlSurface:Verbose-S3-1` (BL-4659) pass too and their KnownFailures lines are removed here (the known-failures check fails on a listed case that passes); `CurlSurface:Verbose-Oci-1` still fails and keeps its line.
- FailWithBody cause: `AzureStorageClient.ExchangeAsync` read the error body only for the exit-22 message. With `--fail-with-body` (`context.Http.Fail == HttpFailMode.FailWithBody`) it now writes the body to the transfer's output before failing, as curl does; with `--fail` or neither it writes nothing, as before. No ADR: the manual states the behaviour.
- ADR-0482 unit tests in `AzureStorageProtocolHandlerTests`: `ExecuteAsync_ErrorWithFailWithBody_WritesTheErrorBodyAndFailsWithExit22`, `ExecuteAsync_ErrorWithFail_WritesNothingAndFailsWithExit22`, `ExecuteAsync_Url_HandsTheTransfersEventsToTheConnectorForVerbosesConnectionLine`.
- Checks: `dotnet build` 0 warnings 0 errors; fast tests 115 projects passed, none failed; Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.AzureStorage.UnitLibrary: 0 failing members. Platform: the connection line is curl's connect filter on every platform and the TLS prefix keeps its per-platform branch, so the change is platform-neutral; CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: c68a4a64 841f2488 f0180dec.
- 2026-11-02: Doing -> Done. curl -v and --fail-with-body for az, abfs and abfss conformance cases pass: the error body is written with --fail-with-body, and the storage -v check expects curl 8.22's Established connection line
