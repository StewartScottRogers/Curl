---
id: BL-4658
title: Fix GoogleCloudStorage conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for gs
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/StorageTransferChecks.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4658 — Fix GoogleCloudStorage conformance: CurlSurface:Verbose and CurlSurface:FailWithBody for gs

## Goal

`curl -v` and `curl --fail-with-body` behave for `gs` as curl's manual says any transfer does, so the BL-4615 cases `CurlSurface:Verbose-Gs-1`, `CurlSurface:FailWithBody-Gs-1` pass.

## Context

BL-4615 ran curl's generic transfer options through `Curl.Console` in process against each storage scheme's reference server (`Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/Storage/`, `GsTransferCases`; shared checks in `StorageTransferChecks`). `-o`, `-O`, `-w`, `--fail` and `-m` pass; two checks fail for every storage scheme:

- **-v** (curl 8.22.0 manual, "-v"): standard error is empty. It must hold the connection line `* Connected to <host> (127.0.0.1) port 443` and the TLS handshake lines (`* SSL connection using ...`), standard output only the object. The HTTP engine's lines come from `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs`; the extension handler's connection path writes none. The same gap holds for all four storage libraries (BL-4657 to BL-4660), so look first for a cause shared through the connection the handler receives; if it is shared, fix it once and close the other three tasks' Verbose lines with it.
- **--fail-with-body** (manual, "--fail-with-body"; ADR-1090, ADR-1093, ADR-1099, ADR-1105: a status of 300 or more is exit 22): exit 22 is right, but standard output is empty; curl writes the error body to standard output before failing. `Curl.Extended.Protocol.Coap.UnitLibrary/Client/CoapProtocolHandler.cs` shows how a handler reads the option.

## Acceptance criteria

- [x] `CurlSurface:Verbose-Gs-1`, `CurlSurface:FailWithBody-Gs-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests` pins each fix (ADR-0482).
- [x] `dotnet build` is clean, the fast tests are green and `Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary` stays within every quality gate.

## Notes

- Verbose: the cause was shared and in the check, not the handler. Curl already writes the connect lines for `gs` (`* Host ... was resolved`, `*   Trying 127.0.0.1:443...`, `* Established connection to storage.googleapis.com (127.0.0.1 port 443) from ...`, the `* schannel:` lines); `StorageTransferChecks.VerboseAsync` expected the pre-8.22 `* Connected to <host> (127.0.0.1) port 443` line. curl 8.22.0 writes `* Established connection to <host> (<ip> port <port>) from ...` (the line the Amqp, Kafka, Nats, Stomp and Realtime checks already pin as measured), so the check now expects that. Added `StorageTransferChecks.cs` to `touches` for it: no task in Doing on origin/work/dark-factory names it.
- With the check fixed, `Verbose-Az-1`, `Verbose-Abfs-1`, `Verbose-Abfss-1` and `Verbose-S3-1` pass too, so their KnownFailures lines are removed here, closing the Verbose half of BL-4657 and BL-4659 (and BL-4660's Abfs lines if it names them). `Verbose-Oci-1` still fails with standard error empty, a cause of the Oci handler's own; its line stays.
- FailWithBody: `GcsClient` read the error body only to name the `Error` code. Under `--fail-with-body` (`context.Http.Fail == HttpFailMode.FailWithBody`, as `CoapProtocolHandler` reads it) it now writes the error body's bytes to the output before failing with exit 22, as curl does; `-f` and no option write nothing (unchanged). Pinned by `ExecuteAsync_ErrorResponseUnderFailWithBody_WritesTheErrorBodyThenFailsWithExit22` and `ExecuteAsync_ErrorResponseWithoutFailWithBody_WritesNothingToTheOutput`. Updated the library's `CLAUDE.md` line for `GcsClient` to say so (added to `touches`).
- Checks: GCS unit tests 125/125, the Storage conformance cases 51/51, `dotnet build` clean, fast tests green, Measure-CodeQuality for the GCS library 0 failing members. Platform-neutral: the TLS prefix in the check already differs per platform; nothing else touches paths or OS text.
## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 0f217622 af799a50 a3b784ac.
- 2026-11-02: Doing -> Done. gs -v passes its check (curl 8.22.0's Established connection line) and --fail-with-body writes the error body; Az, Abfs, Abfss and S3 Verbose cases pass too
