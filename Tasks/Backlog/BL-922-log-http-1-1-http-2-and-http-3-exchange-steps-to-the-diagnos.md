---
id: BL-922
title: Log HTTP/1.1, HTTP/2 and HTTP/3 exchange steps to the diagnostic log in Curl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-916]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-922 — Log HTTP/1.1, HTTP/2 and HTTP/3 exchange steps to the diagnostic log in Curl.Protocol.Http

## Goal

`Curl.Protocol.Http.UnitLibrary` writes the diagnostic log from `ITransferContext.DiagnosticLog` for each HTTP exchange (version chosen, request sent, reply read, body framing, connection kept or closed) under component `http`, `http2` or `http3` by the version in use.

## Context

- The rules are BL-915's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-916's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `HttpProtocolHandler.cs` (version selection and fallback, request and reply milestones, authentication rounds, the 100-continue wait, connection reuse), `HttpResponseHeadReader.cs`, `HttpResponseBodyReader.cs` (chunked, content-length or close-delimited, decoders chosen), `HttpRequestBodyWriter.cs`, `Http2Session.cs`/`Http2StreamConnection.cs` and `Http3Session.cs`/`Http3StreamConnection.cs` (stream opened, SETTINGS, GOAWAY or RST_STREAM received). `Curl.Http2` and `Curl.Http3` get no project reference to the abstractions: log around their calls here.
- What, per level: `error` a failed exchange with its `CurlExitCode` and exception; `warning` a downgrade (h2 refused, h3 fallback), a malformed header ignored, a request retried on a closed reused connection; `info` method and URL path sent, status line received, body length and framing, elapsed ms per exchange; `verbose` each header name sent and received (names only; values only for headers other than `Authorization`, `Proxy-Authorization`, `Cookie` and `Set-Cookie`), each HTTP/2 or HTTP/3 frame type and stream ID, the decoder chain.
- Credential-bearing paths: `-u user:secret` Basic, `-H "Authorization: Bearer secret"`, and `-b name=secret`.

## Acceptance criteria

- [ ] `Curl.Protocol.Http.UnitTests` pin: a GET over HTTP/1.1 logs `info` lines for the request and the `200` reply with component `http`; a chunked body logs its framing at `info`; at `verbose` a header name appears and an `Authorization` value does not; an HTTP/2 exchange logs with component `http2`; a connection closed mid-exchange logs `error` with its `CurlExitCode`.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
