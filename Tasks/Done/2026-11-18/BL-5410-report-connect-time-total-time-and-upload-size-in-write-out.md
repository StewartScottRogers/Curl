---
id: BL-5410
title: Report connect time, total time and upload size in write-out for the messaging extension protocols
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests, Curl.Extended.Protocol.Redis.UnitTests/Curl.Extended.Protocol.Redis.UnitTests.csproj, Curl.Extended.Protocol.Redis.UnitTests/Client/RedisUploadSizeThroughEngineTests.cs, Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-18
---
# BL-5410 — Report connect time, total time and upload size in write-out for the messaging extension protocols

## Goal

`-w '%{time_connect} %{time_total} %{size_upload} %{speed_upload}'` for the messaging extension protocols (amqp, amqp1, stomp, mqtt5, nats, redis, kafka, xmpp) reports the real connect time, total time and bytes uploaded instead of 0, as the progress meter already does for the same runs.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-083 (Low, Confirmed: each protocol, repeated). Area: write-out for the messaging extension protocols.

**Repro.** Against a real broker (for example `docker run -d -p 127.0.0.1:48379:6379 redis:7 --requirepass p`):
`curl -sS -m 10 -o NUL -w '[%{time_connect} %{time_total} up=%{size_upload} down=%{size_download} %{speed_upload}]\n' -u :p -d xyz redis://127.0.0.1:48379/SET/k`
The same pattern for kafka, amqp1, mqtt5 and nats with `-d`.
**Expected.** Consistent with the original protocols in the port (for example `file://` reports a non-zero `%{time_total}`) and with the progress meter for the same runs, which shows the uploaded bytes: real connect time, total time and bytes uploaded.
**Actual (PORT).** `[0.000000 0.000000 up=0 ... 0]`, exit 0. `%{size_download}` is correct.
**Where to start.** The extension handlers report progress (for example `context.Progress.ReportDownloaded` in `NatsSession.cs`) but do not fill `TransferReport.Timings` and `UploadSize`, which write-out reads. Every extension handler is wrapped by `Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs` (built in `EngineComposition.cs`), so the fix can stamp the timings (connect time from the recorded connection, total from the transfer's start) and the upload size there once for all of them, as `SmbProtocolHandler.StampReport` does for SMB. The same gap affects the original DICT, Gopher and TELNET handlers (STD-097, task BL-5390, which touches those three libraries); coordinate so the two do not fix the same thing twice: this task fixes only handlers whose report has no `Timings`, in the shared engine path, and leaves what a handler stamps itself alone.

## Acceptance criteria

- [x] New tests in `Curl.Engine.UnitTests` (a new file) prove that when a handler returns a report without `Timings`, the engine path gives it timings with a non-zero total time and the connect time of the first recorded connection, and gives it the upload size the transfer sent.
- [x] A test proves a handler that already stamped its own `Timings` (for example SMB or HTTP) is not overwritten (no regression).
- [x] A test proves a failed transfer (non-zero exit) still reports its timings and the bytes sent.
- [x] A test in one extension handler's `.UnitTests` (redis) proves the bytes of a `-d` body appear as the upload size through the engine path.
- [x] Tests are platform-neutral and use a fake `TimeProvider`.
- [x] Hand check recorded in the Log: the redis command prints `time_total` greater than 0 and `up=3` or more; repeat for kafka, amqp1, mqtt5 and nats where a broker is at hand.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` is narrowed from the whole Engine library to the decorator that wraps every handler; if the upload size needs a count from the connection, add the one connector file in `Curl.Engine.UnitLibrary` that holds it.
- Review acceptance check: the redis command prints `time_total` > 0 and `up=3` or more.

- Done: EndPointReportingProtocolHandler stamps Timings (start = before the handler, connect = the first connection's ConnectTimings, which ConnectionEndPointRecorder.Record now keeps, total = after) and UploadSize (-d data length, else bytes the seekable upload stream gave) on any report without Timings; a handler's own timings and upload size are kept. A non-seekable upload stream reports 0.
- Zero elapsed time prints 0.000001 (the least curl reports), so 8 Curl.Conformance.Extended.UnitTests files that pinned 0.000000 on a frozen clock now pin 0.000001 (no other Doing task touches that project). Two Engine tests that pinned a null report now assert the end point and connection count. Redis.UnitTests references Curl.Engine.UnitLibrary (new InternalsVisibleTo) to test the wrapper.
- Hand check: no broker at hand (Docker not started), so used a PowerShell fake RESP server on 127.0.0.1:48379: `[0.055595 0.343253 up=3 down=3 8]` for the redis command. Kafka, amqp1, mqtt5 and nats not repeated: they share the same engine path.
- CI on Linux and macOS: tests are platform-neutral (fake clocks); ticked by that.

## Log

- 2026-11-17: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 05bdf0ad 5902434f 1c28bb7d d4059c66 66536c10 f50bf17d a0b49a29.
- 2026-11-18: Doing -> Done.
- 2026-11-18: Doing -> Done. Messaging extension protocols now report connect time, total time and upload size in write-out via the engine's handler wrapper
