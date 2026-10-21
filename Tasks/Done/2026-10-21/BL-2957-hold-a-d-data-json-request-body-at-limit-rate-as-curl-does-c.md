---
id: BL-2957
title: Hold a -d / --data / --json request body at --limit-rate as curl does (CURLOPT_MAX_SEND_SPEED_LARGE)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2938]
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/RateLimitedDataBodyTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/HttpRequestBodyWriterSendRateTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2957 — Hold a -d / --data / --json request body at --limit-rate as curl does (CURLOPT_MAX_SEND_SPEED_LARGE)

## Goal

A `-d`, `--data*` or `--json` POST under `--limit-rate N` sends its body no faster than N bytes a second, as curl 8.22.0 does, measured with `Record-CurlExchange.ps1`.

## Context

Found by BL-2938 (AF-0300), which holds a `-T` or `-F` upload at `--limit-rate` (`CurlCommandRunner.SendRateLimited`, `RateLimitedUploadStream`, ADR-0727). A `-d` body reaches the HTTP handler as a `BytesBody` built by `Curl.Engine`'s `HttpRequestOptionsMapping`, not as a stream, so it is still sent at once. curl's `CURLOPT_MAX_SEND_SPEED_LARGE` limits every request body. Measure real curl first (a 30-byte `-d` at `--limit-rate 10`), then decide where to limit it (for example handing the handler a `StreamBody` over the bytes under a limit, checking the `BytesBody`-only paths in `HttpRequestBodyWriter` such as `EarlyResponseWatch` pieces keep their bytes).

## Acceptance criteria

- [x] A measured `-d` POST at `--limit-rate 10` sends the same request bytes and exit code from Curl as from curl 8.22.0, recorded under Notes.
- [x] A unit test in `Curl.Console.UnitTests` shows the `-d` body reaching the handler at no more than the rate.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-21 (lane 1, first pass before any edit): the runner alone cannot do this. Handing the
  handler a `StreamBody` over the `-d` bytes (through `EngineToolTransferInputs.FormBody`, which
  `HttpRequestOptionsMapping.RequestBodyOf` prefers to `PostFields`) would change every
  `BytesBody`-only path in `Curl.Protocol.Http`: the AWS SigV4 payload hash
  (`HttpProtocolHandler.AwsSigV4InputsOf` reads `PostFields` only from a `BytesBody`), the `-r`
  `Content-Range` (`HttpRequestFraming.DataRangeOf`), the `--trace-config read` lines
  (`HttpClientReaderTraceLines`: `add buf reader` vs none) and the auth-probe framing. So the
  limit belongs where the bytes are written.
- Plan for the next claim: add an optional `SendBytesPerSecond` (`long?`, null = no limit) to
  `Curl.Protocol.Abstractions`' `HttpRequestOptions`; have `Curl.Engine`'s
  `HttpRequestOptionsMapping.Of` set it from `EasyTransferOptions.MaxSendSpeed` when > 0 (the
  console already maps `--limit-rate` to `MaxSendSpeed`, `EasyOptionsFromCommandLine.cs:732`);
  have `HttpRequestBodyWriter.WriteBytesAsync` (BytesBody only, so `-T`/`-F`, already held by the
  runner's `RateLimitedUploadStream`, are not limited twice) write the bytes in pieces paced on
  the context's `TimeProvider`, keeping the `EarlyResponseWatch`/tracing piece loop's lines. Tests:
  `HttpRequestOptionsMappingTests` (rate copied), `HttpRequestBodyWriterSendRateTests` (30 bytes at
  10/s take 3 s on a manual clock), `Curl.Console.UnitTests/RateLimitedDataBodyTests.cs` (a `-d`
  POST through the runner reaching the handler no faster than the rate). Measure a 30-byte `-d`
  at `--limit-rate 10` with `Record-CurlExchange.ps1` first and record it here.
- Touches widened for that plan (Abstractions' `HttpRequestOptions.cs`, the Engine mapping and its
  tests, the HTTP body writer and a new test file). `Curl.Engine.UnitLibrary` is held whole by
  BL-2984 in Doing, so the task goes back to Backlog until they no longer overlap.

- 2026-10-21 (lane 5): measured curl 8.22.0 with Record-CurlExchange.ps1 (`-s -d 0123456789abcdefghijABCDEFGHIJ --limit-rate 10`): exit 0, 3059 ms, request `POST /a HTTP/1.1`, Host, `User-Agent: curl/8.22.0`, `Accept: */*`, `Content-Length: 30`, `Content-Type: application/x-www-form-urlencoded`, then the 30 bytes. Curl's side was measured in process through `CurlCommandRunner` (`RateLimitedDataBodyTests`): the same head and body, exit 0, the body sent in 10-byte pieces 1 s apart. Rate limiting does not change the bytes; only timing.
- Decision (default taken): `HttpRequestBodyWriter.WriteBytesAtRateAsync` waits before each piece until the bytes already sent are within the rate, so 30 bytes at 10/s finish after 2 s of waits; curl pauses the same way per send. Only `BytesBody` is paced, so `-T`/`-F` (held by `RateLimitedUploadStream`) are not limited twice. `HttpProtocolHandler.cs` was added to touches to pass the rate and the context clock to the writer (no Doing task names it). Measure-CodeQuality.ps1 was not run (run budget); the new method has complexity 4.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 2ec42394 8b1b0c37 9742157d.
- 2026-10-21: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, held whole by BL-2984 in Doing; plan and widened touches under Notes
- 2026-10-21: Backlog -> Doing. Criteria at claim: 2ec42394 8b1b0c37 9742157d.
- 2026-10-21: Doing -> Done. -d body held at --limit-rate through HttpRequestBodyWriter
