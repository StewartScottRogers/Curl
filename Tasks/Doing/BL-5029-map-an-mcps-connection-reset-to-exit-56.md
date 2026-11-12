---
id: BL-5029
title: Map an mcps connection reset to exit 56
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpStreamableHttpTransport.cs, Curl.Extended.Protocol.McpClient.UnitTests/StreamableHttp/McpStreamableHttpTransportTests.cs, Documentation/Conformance/Extended/McpClient.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5029 — Map an mcps connection reset to exit 56

## Goal

A connection reset during an `mcps://` (and `mcp://` Streamable HTTP) call exits 56 with a `curl: (56) Recv failure: Connection was reset` line (55 when the reset happens while sending), like every other extension scheme, instead of exit 2 with raw .NET text.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-032, mcps part (Medium, Confirmed). The grpc/grpcs part is BL-5028.

**Repro.** Run a TLS server on 127.0.0.1:46040 with a self-signed certificate that completes the handshake, reads once with a 2 s timeout, and closes. Run `curl -sS -k -m 6 mcps://127.0.0.1:46040/m`.

**Expected.** Every other extension scheme against the same servers exits 56: nats, socketio and signalr print `curl: (56) Recv failure: Connection was reset`; stomp, redis, amqp, amqp1, mqtt5, mqtt5+ws and kafka exit 56; xmpps, stomps, natss, kafkas, rediss, amqp1s, amqps, mqtt5s, socketios and signalrs exit 56.

**Actual.** `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..` (raw .NET text, doubled period), exit 2.

**Where to start.** `Curl.Extended.Protocol.McpClient.UnitLibrary/StreamableHttp/McpStreamableHttpTransport.cs` has the same gap as grpc: it does not catch `IOException` or `SocketException`. Map them to 56, or to 55 when sending. `StreamableHttp/McpHttpException.cs` and `Client/McpClientException.cs` carry the exit code the handler reports, so the mapping may belong there; add those files to `touches` if the run needs them.

## Acceptance criteria

- [ ] Tests in `Curl.Extended.Protocol.McpClient.UnitTests/StreamableHttp/McpStreamableHttpTransportTests.cs` script a connection (`RecordedConnection`) that throws `IOException` wrapping a `SocketException` (connection reset) while reading the reply, and assert exit 56 with the message `Recv failure: Connection was reset`.
- [ ] A test scripts the reset while the request is being written and asserts exit 55.
- [ ] The messages contain no raw .NET text and no doubled period.
- [ ] Regression: the existing HTTP failure mappings (`McpHttpException`, `McpHttpFailure`) keep their current exit codes and messages.
- [ ] `Documentation/Conformance/Extended/McpClient.md` gains a row stating the reset-to-56 (55 when sending) mapping, its "Covered by" naming the new tests.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: `mcps://127.0.0.1:46040/m` prints `curl: (56) Recv failure: Connection was reset` (or a similar `(56)` line) and exits 56.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 2bfaea6d 87757494 b0fc451b 326e1f61 8834f53f a0b49a29.
