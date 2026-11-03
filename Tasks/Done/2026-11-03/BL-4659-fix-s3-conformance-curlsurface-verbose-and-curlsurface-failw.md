---
id: BL-4659
title: Fix S3 conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for s3
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitLibrary/Client, Curl.Extended.Protocol.S3.UnitLibrary/CLAUDE.md, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs, Curl.Extended.Protocol.S3.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4659 — Fix S3 conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for s3

## Goal

`curl -v` and `curl --fail-with-body` behave for `s3` as curl's manual says any transfer does, so the BL-4615 cases `CurlSurface:Verbose-S3-1`, `CurlSurface:FailWithBody-S3-1` pass.

## Context

BL-4615 ran curl's generic transfer options through `Curl.Console` in process against each storage scheme's reference server (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/`, `S3TransferCases`; shared checks in `StorageTransferChecks`). `-o`, `-O`, `-w`, `--fail` and `-m` pass; two checks fail for every storage scheme:

- **-v** (curl 8.22.0 manual, "-v"): standard error is empty. It must hold the connection line `* Connected to <host> (127.0.0.1) port 443` and the TLS handshake lines (`* SSL connection using ...`), standard output only the object. The HTTP engine's lines come from `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs`; the extension handler's connection path writes none. The same gap holds for all four storage libraries (BL-4657 to BL-4660), so look first for a cause shared through the connection the handler receives; if it is shared, fix it once and close the other three tasks' Verbose lines with it.
- **--fail-with-body** (manual, "--fail-with-body"; ADR-1090, ADR-1093, ADR-1099, ADR-1105: a status of 300 or more is exit 22): exit 22 is right, but standard output is empty; curl writes the error body to standard output before failing. `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs` shows how a handler reads the option.

## Acceptance criteria

- [x] `CurlSurface:Verbose-S3-1`, `CurlSurface:FailWithBody-S3-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.S3.UnitTests` pins each fix (ADR-0482).
- [x] `dotnet build` is clean, the fast tests are green and `Curl.Extended.Protocol.S3.UnitLibrary` stays within every quality gate.

## Notes

- Verbose: Curl already wrote the connection and TLS lines for s3. The shared check `StorageTransferChecks.VerboseAsync` expected curl's old `* Connected to <host> (127.0.0.1) port 443` line, but curl 8.22.0 writes `* Established connection to <host> (127.0.0.1 port 443) from ...` (the line the Amqp0, Amqp1, Kafka, Nats, Stomp, Redis and Realtime checks pin, measured there). So the fix is to the check, not to S3. `StorageTransferChecks.cs` was added to touches because the cause is shared; no task in Doing on origin/work/dark-factory named it. With this fix Verbose-Az-1, Verbose-Abfs-1, Verbose-Abfss-1 and Verbose-Gs-1 pass too, so their KnownFailures lines are removed (the Verbose halves of BL-4657 and BL-4658). Verbose-Oci-1 still fails for a cause of its own and keeps its line for BL-4660.
- FailWithBody: `S3Client` now reads a failing response's body as bytes and, when `context.Http.Fail` is `FailWithBody`, writes those bytes to the output before throwing the exit-22 failure, the way `CoapProtocolHandler` reads the option. Under `-f` or no fail option nothing is written; ADR-1090's exit 22 for every status of 300 or more is unchanged. Pinned by `S3ProtocolHandlerTests.ExecuteAsync_ErrorResponse_WritesTheErrorBodyOnlyUnderFailWithBody`. `ExecuteAsync_Connect_HandsTheConnectorTheTransfersEventsAndLog` pins that the connection path passes the transfer's events, which carry the -v lines. No ADR: both changes follow curl's manual and the measured line, and make no new decision.
- Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.S3.UnitLibrary: 0 failing members. dotnet build is clean, and the fast tests are green on Windows. The change is platform-neutral, since the check's TLS prefix already depends on the platform.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b2b7ba2b b2a2ed3a 770bd381.
- 2026-11-03: Doing -> Done. Verbose-S3-1 and FailWithBody-S3-1 pass; the shared Verbose check fix also closes Verbose-Az, Abfs, Abfss and Gs
