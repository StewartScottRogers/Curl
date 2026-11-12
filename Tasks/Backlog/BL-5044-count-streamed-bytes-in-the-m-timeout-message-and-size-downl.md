---
id: BL-5044
title: Count streamed bytes in the -m timeout message and size_download for nats and signalr
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs, Curl.Extended.Protocol.Nats.UnitTests/NatsProtocolHandlerTests.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Nats.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5044 — Count streamed bytes in the -m timeout message and size_download for nats and signalr

## Goal

A `-m` timeout after nats or signalr has streamed messages reports `with N bytes received` (N the bytes written) in the exit-28 message, and `%{size_download}` equals N.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-036 (Low, Confirmed).

**Repro.** Run a NATS mock on 127.0.0.1:47182 that sends INFO, answers SUB with `MSG x 1 2\r\nok\r\n`, then stays silent. Run `curl -sS -m 1.5 -w '\nsize_download=%{size_download}\n' nats://127.0.0.1:47182/x`.

**Expected.** `Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Nats.md` NatsWriteOut-1 says `%{size_download}` is the bytes written. The port's HTTP path says `... with 5 out of 100 bytes received` in the same situation. So the text is `Operation timed out after N milliseconds with 3 bytes received` and `size_download=3`.

**Actual.** stdout holds `ok`, then `curl: (28) Operation timed out after 1506 milliseconds with 0 bytes received`, then `size_download=0`. SignalR behaves the same after one hub call. When the transfer ends normally (`?count=1`), the count is right.

**Where to start.** Checked in the repository: the message is built by `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs` (`OperationTimedOutMessage`) from `bytesReceived`, which only `ITransferProgress.ReportDownloaded(bytesSoFar, expectedTotal)` sets. Stomp already calls it after each write (`Curl.Extended.Protocol.Stomp.UnitLibrary/Client/StompClientFlow.cs`, `WriteOutputAsync`); `Curl.Extended.Protocol.Nats.UnitLibrary/NatsSession.cs` (writes at lines 110 and 117) and `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs` (writes at line 133) do not. Call `context.Progress.ReportDownloaded` with the running byte total after each write in both, as Stomp does. No change to Core is expected.

## Acceptance criteria

- [ ] `Curl.Extended.Protocol.Nats.UnitTests/NatsProtocolHandlerTests.cs` has a test with a scripted connection that sends `MSG x 1 2` / `ok`, then stays silent until the cancellation fires: the recording progress sees `ReportDownloaded(3, null)` (`ok` plus the `\n` the session writes), so the watchdog's message reads `with 3 bytes received`.
- [ ] `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs` has the equivalent test after one hub message line is written (the line's byte count, then silence, then timeout).
- [ ] A Console-level or Core-level assertion (an existing `-m` test pattern such as `CurlCommandRunnerMaxTimeTests`, or a nats handler test with `MaxTimeWatchdog`) shows `%{size_download}` equals the same count after the timeout.
- [ ] Regression: a normal end (`?count=1`) still reports the right size, and a timeout before any message still says `with 0 bytes received`.
- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Nats.md` NatsWriteOut-1 (and `SignalR.md` if it lists the case) states the timeout case.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the libraries' scripted connections. The review says "the shared extended-protocol transfer path"; the repository shows no such shared path, the cause is per-handler (missing `ReportDownloaded`), so `touches` name the two session files. Other extended protocols that stream messages and do not call `ReportDownloaded` may have the same gap; file a follow-up task rather than widening this one.

## Log

- 2026-11-12: Created.
