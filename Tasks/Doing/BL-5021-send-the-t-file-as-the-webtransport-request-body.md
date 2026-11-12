---
id: BL-5021
title: Send the -T file as the webtransport request body
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportProtocolHandler.cs, Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportProtocolHandlerTests.cs, Documentation/Conformance/Extended/WebTransport.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5021 — Send the -T file as the webtransport request body

## Goal

`curl -T file webtransport://...` sends the file's contents as the request body, exactly as `-d` would, instead of building its request from `-d` data alone.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-031, webtransport part (Medium, Probable: found from the source only, not reproduced against a mock). Data loss / silent wrong operation: yes. This task is only the webtransport part; grpc, signalr and socketio are BL-5018, BL-5019 and BL-5020.

**Expected.** The port's own convention, already applied to nats, stomp, mqtt5, redis, kafka, amqp and amqp1, is that `-T file` sends the file exactly as `-d` would. See `Documentation/Conformance/Extended/Nats.md` ("What `-T` does"), `Mqtt5.md` ("`-T` (upload)") and `Stomp.md` STOMP12-Send-3. A scheme that cannot use `-T` must instead fail with exit 2 before connecting; for webtransport the convention (send the file) is the intended answer.

**Actual (from the source).** `Curl.Extended.Protocol.WebTransport.UnitLibrary/WebTransportProtocolHandler.cs` line 59 builds its request from `context.PostData` only, so a `-T` file is ignored.

**Where to start.** That line: read `context.Upload` when `PostData` is null, as `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs` line 93 does. First confirm the finding with a unit test that fails before the change; if the handler already honours `Upload` somewhere the review missed, say so in the Log and make the tests below pin it.

## Acceptance criteria

- [ ] A test in `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportProtocolHandlerTests.cs` with an upload of a known payload asserts the request body sent on the WebTransport stream equals the file's bytes (the same bytes `-d` with that payload sends).
- [ ] Regression: `-d` still sends its payload, and `-d` wins when both `-d` and `-T` are given; with neither, behaviour is unchanged.
- [ ] `Documentation/Conformance/Extended/WebTransport.md` gains a row stating `-T` sends the file as the request body exactly as `-d` does, its "Covered by" naming the new test.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `QuicTestServer` / `ScriptedHttp3Connection` stand-ins in `Curl.Extended.Protocol.WebTransport.UnitTests`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: bf16039e 47a4d9c7 8f31984b a0b49a29.
