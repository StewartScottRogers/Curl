---
id: BL-835
title: Race QUIC against TCP for --http3 with the happy-eyeballs timeout
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-835 — Race QUIC against TCP for --http3 with the happy-eyeballs timeout

## Goal

With `--http3` on an `https://` URL and no proxy, `HttpProtocolHandler` starts the TCP attempt as soon as the QUIC attempt fails or once the happy-eyeballs timeout (default 200 ms) has passed on the injected `TimeProvider` without the QUIC handshake completing, whichever comes first; the first attempt to succeed carries the transfer and the other is cancelled and disposed.

## Context

- Today `HttpProtocolHandler.ConnectAsync` (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`) awaits `plan.Deadline.ConnectMultiplexedAsync(connector, target)` and only when that fails calls `plan.Deadline.ConnectAsync(connector, target)`. BL-731 built it that way on purpose (ADR-0172, `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`, written by BL-731).
- The rule to implement is ADR-0144 section 4 (`Documentation/Planning/Decisions/ADR-0144-http-3-is-hand-built-over-a-hand-built-quic-and-http3-races-tcp-as-curls-ngtcp2-build-does.md`): the TCP attempt (TLS with ALPN `h2,http/1.1`) starts when QUIC fails or when `--happy-eyeballs-timeout-ms` (default 200 ms) elapses without the QUIC handshake completing; first success wins; when both fail the transfer fails with the QUIC attempt's exit code and message (already implemented by BL-731; keep it). ADR-0144's measurements: `--http3 --happy-eyeballs-timeout-ms 1000` against a silent UDP peer connected over TCP with `%{time_connect}` 1.007 s; the source is `lib/cf-https-connect.c` at curl tag `curl-8_18_0` (curl.se's Windows build 8.18.0 with ngtcp2 1.21.0 is the measured HTTP/3 reference). The soft timeout (a quarter of the value) never fires for ngtcp2, so only the hard timeout is implemented.
- The timeout does not reach the handler today. `happy-eyeballs-timeout-ms` appears only in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs` and `Curl.Cli.UnitLibrary/CurlHelpTable.cs`; `Curl.Protocol.Abstractions.UnitLibrary` has no member for it. BL-644 (Backlog) parses the option and races address families inside `Curl.Networking.UnitLibrary/TcpConnector.cs`, but adds nothing to the abstractions. So this task adds `public TimeSpan HappyEyeballsTimeout { get; init; } = TimeSpan.FromMilliseconds(200);` to `HttpRequestOptions` (`Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs`), not to `ITransferContext`, whose many implementers across the protocol libraries would all have to change. Filling it from the parsed option in `Curl.Console/HttpRequestOptionsMapping.cs` is outside this task (`Curl.Console` is not in `touches`); until that lands every transfer uses the 200 ms default, which is curl's default.
- The race must be driven only by the injected `TimeProvider` (`plan.Context.TimeProvider`), never `Task.Delay` without it or `Thread.Sleep`, and must keep `plan.Deadline`'s `--connect-timeout`/`-m` behaviour. `--http3-only` never starts a TCP attempt; `http://` URLs and proxies never try QUIC (`TriesQuic`), unchanged.
- The loser must be disposed: a QUIC connection that completes after TCP won is closed (`IMultiplexedConnection.DisposeAsync`), and a TCP connection that completes after QUIC won is disposed.

## Acceptance criteria

- [x] `Curl.Protocol.Abstractions.UnitTests/HttpRequestOptionsTests.cs` has a test showing `new HttpRequestOptions().HappyEyeballsTimeout` is 200 ms.
- [x] `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` (or a new `HttpProtocolHandlerTests.Http3Race.cs`) has tests on a fake `TimeProvider` and fake connectors that pin: the TCP dial has not started 199 ms after the QUIC dial with the handshake pending, and has started at 200 ms; with `HappyEyeballsTimeout` set to 1000 ms it starts at 1000 ms; a QUIC failure before the timeout starts TCP at once; TCP winning disposes the pending QUIC attempt (cancelled and, if it later completes, its connection disposed) and the response comes over TCP; QUIC completing after the timeout but before TCP carries the transfer as HTTP/3 and the TCP attempt is cancelled or disposed; both failing reports the QUIC attempt's exit code and message; `--http3-only` never dials TCP however long the handshake takes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` each report 100% line and branch coverage and no failing member.

## Notes

- Plan as built: `HttpRequestOptions.HappyEyeballsTimeout` (default 200 ms). `HttpProtocolHandler.ConnectAsync` sends `--http3-only` straight to QUIC and `--http3` to `RaceQuicAgainstTcpAsync`, which starts QUIC, waits for it or a `Task.Delay(HappyEyeballsTimeout, context.TimeProvider)`, returns an early QUIC connection, otherwise starts TCP; TCP connecting first wins, else the QUIC result decides, else the TCP result, else the QUIC failure with TCP's timings and connection number (BL-731's rule, kept). The loser's `CancellationTokenSource` is cancelled and a fire-and-forget `DisposeLosingTcpAsync`/`DisposeLosingQuicAsync` disposes any connection it still opens.
- `HttpTransferDeadline.ConnectAsync`/`ConnectMultiplexedAsync` take an optional `abandoned` token linked into the connect's token, so `--connect-timeout` and `-m` still hold for both attempts; an abandoned connect ends as the deadline's exit 28 result, which the race discards.
- Choice: the post-TCP-start logic checks "TCP connected first" and otherwise takes QUIC's result, so "TCP failed then QUIC connected" and "QUIC connected first" share one path. That keeps the outcome independent of which completion `Task.WhenAny` sees when both land at once, and keeps the tests deterministic.
- Choice: no new ADR. The behaviour is ADR-0144 section 4, already decided; ADR-0172's "only when QUIC fails" is amended in place (added `Documentation/Planning/Decisions/ADR-0172-...md` to `touches`; no task in Doing names it).
- Test fakes: `Fakes/RacingConnector.cs` (TCS-driven QUIC and TCP connects that record their tokens), plus a `Disposed` task on `ScriptedConnection` and `FakeMultiplexedConnection` so a test can wait for a loser's disposal. `Http3Context` gained a `connectTimeout` parameter so race tests use 60 s and never collide with the happy-eyeballs timer.
- Follow-up filed: BL-889 fills `HappyEyeballsTimeout` from `--happy-eyeballs-timeout-ms` in `Curl.Console` (depends on BL-644).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --http3 races QUIC against TCP: TCP starts when QUIC fails or after the 200 ms happy-eyeballs timeout, first connect wins, the loser is cancelled and disposed
