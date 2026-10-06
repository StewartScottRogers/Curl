---
id: BL-922
title: Log HTTP/1.1, HTTP/2 and HTTP/3 exchange steps to the diagnostic log in Curl.Protocol.Http
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-922 — Log HTTP/1.1, HTTP/2 and HTTP/3 exchange steps to the diagnostic log in Curl.Protocol.Http

## Goal

`Curl.Protocol.Http.UnitLibrary` writes the diagnostic log from `ITransferContext.DiagnosticLog` for each HTTP exchange (version chosen, request sent, reply read, body framing, connection kept or closed) under component `http`, `http2` or `http3` by the version in use.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `HttpProtocolHandler.cs` (version selection and fallback, request and reply milestones, authentication rounds, the 100-continue wait, connection reuse), `HttpResponseHeadReader.cs`, `HttpResponseBodyReader.cs` (chunked, content-length or close-delimited, decoders chosen), `HttpRequestBodyWriter.cs`, `Http2Session.cs`/`Http2StreamConnection.cs` and `Http3Session.cs`/`Http3StreamConnection.cs` (stream opened, SETTINGS, GOAWAY or RST_STREAM received). `Curl.Http2` and `Curl.Http3` get no project reference to the abstractions: log around their calls here.
- What, per level: `error` a failed exchange with its `CurlExitCode` and exception; `warning` a downgrade (h2 refused, h3 fallback), a malformed header ignored, a request retried on a closed reused connection; `info` method and URL path sent, status line received, body length and framing, elapsed ms per exchange; `verbose` each header name sent and received (names only; values only for headers other than `Authorization`, `Proxy-Authorization`, `Cookie` and `Set-Cookie`), each HTTP/2 or HTTP/3 frame type and stream ID, the decoder chain.
- Credential-bearing paths: `-u user:secret` Basic, `-H "Authorization: Bearer secret"`, and `-b name=secret`.

## Acceptance criteria

- [x] `Curl.Protocol.Http.UnitTests` pin: a GET over HTTP/1.1 logs `info` lines for the request and the `200` reply with component `http`; a chunked body logs its framing at `info`; at `verbose` a header name appears and an `Authorization` value does not; an HTTP/2 exchange logs with component `http2`; a connection closed mid-exchange logs `error` with its `CurlExitCode`.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- 2026-09-29 (lane 5): `Curl.Console.UnitTests` added to `touches`. The work makes
  `CurlCommandRunnerDiagnosticLogTests.RunAsync_LogLevelInfoUnderSilent_WritesTheLinesToStandardError`
  fail: it pins exactly 3 lines at `--log-level info` over a loopback HTTP GET, and the http
  component now adds 4 (`GET / sent`, `reply HTTP/1.1 200 OK`, `body framed by Content-Length 5`,
  `exchange done: ...`), so 7. The fix is to expect 7 (or assert the http lines too). BL-736,
  in Doing, touches `Curl.Console.UnitTests`, so the task went back to Backlog until it is Done.
- Design used on the first attempt (build clean, all other fast tests green, 38 new tests):
  one `internal sealed class HttpExchangeLog(IDiagnosticLog, string component)` in the library,
  made per exchange with `HttpExchangeLog.For(context.DiagnosticLog, streams)` (component from
  `IHttpStreamSession.VersionName`: `http2`, `http3`, else `http`). Methods, each testing
  `IsEnabled` first: `VersionChosen` (verbose `using HTTP/x on a new|reused connection`; warning
  for `--http3` that fell back to TCP, and `--http2` over TLS left on HTTP/1.x by ALPN),
  `RequestSent(method, Url.AbsolutePath, head bytes)` (info `GET /path sent`; verbose each
  request header, values of `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`
  written as `(value not logged)`), `ReplyRead(actedOn head)` (info `reply <status line>`;
  verbose each reply header), `BodyFramed` called from `HttpResponseBodyReader.CopyAsync` through
  a new `Log` property (info `body framed chunked|by Content-Length N|until the connection
  closes`; verbose the decoder chain), `Exchanged` (info status, body bytes, ms since
  `RequestReady`; reads the clock only when enabled so existing time-stepping tests are
  unchanged), `Failed` (error `exchange failed with <CurlExitCode> (exit N): <message>
  [<exception type>: <message>]`, from both catch blocks of `ExchangeAsync`), and
  `RetryingOnFreshConnection` (warning, in `SettleConnection`'s died-before-response branch).
  Tests: `HttpExchangeLogTests` and `HttpProtocolHandlerTests.DiagnosticLog.cs` with
  `Fakes/RecordingDiagnosticLog` (copied from Rtsp) and `Fakes/StubStreamSession`.
- Frame-level lines (each HTTP/2 or HTTP/3 frame type and stream ID, SETTINGS, GOAWAY,
  RST_STREAM) need hooks inside `Http2Session`/`Http3Session`; not in the acceptance criteria,
  so the first attempt left them for a follow-up task.
- 2026-10-01 (lane 1): Rebuilt (the first attempt's code was not in this checkout). `HttpExchangeLog` in the library, made per exchange by `HttpExchangeLog.For(context.DiagnosticLog, streams)`: verbose `using HTTP/x on a new|reused connection`; warning `--http3 fell back to ... over TCP` and `--http2 refused by ALPN; using HTTP/1.x` (new connections only); info `GET /path sent` (path only, never the query), `reply <code> <reason>`, `body framed chunked|by Content-Length N|until the connection closes`, `exchange done: status N, B body bytes in M ms` (clock read only when info is enabled); verbose `header sent|received Name: value` with `Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie` values written `(value not logged)`, and `decoders a, b`; error `exchange failed with <CurlExitCode> (exit N): <message> [<exception type>]` from both catch blocks; warning on a reused connection that died before its reply. `HttpResponseBodyReader.Log` carries it to the framing step. Tests: `HttpExchangeLogTests` (every branch), `HttpProtocolHandlerTests.DiagnosticLog.cs` (the acceptance cases; -b is driven through `ScriptedCookieStore`, -u through `ScriptedAuthenticator` with `Credentials`), `Fakes/RecordingDiagnosticLog` copied from Rtsp. `CurlCommandRunnerDiagnosticLogTests.RunAsync_LogLevelInfoUnderSilent_WritesTheLinesToStandardError` now expects 7 lines and the `[http] GET / sent` line (BL-736 is no longer in Doing). Coverage 100/100, 0 failing members. Frame-level lines filed as BL-1073.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Needs Curl.Console.UnitTests (its loopback --log-level info test pins 3 lines; the http lines make 7), which BL-736 in Doing touches
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. HTTP/1.1, HTTP/2 and HTTP/3 exchanges write their steps to --log-level under http, http2 or http3, with no credential in any line
