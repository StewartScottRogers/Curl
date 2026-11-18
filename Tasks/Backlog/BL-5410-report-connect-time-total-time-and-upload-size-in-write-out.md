---
id: BL-5410
title: Report connect time, total time and upload size in write-out for the messaging extension protocols
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Curl.Engine.UnitTests, Curl.Extended.Protocol.Redis.UnitTests]
requirement: none
created: 2026-11-17
completed:
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

- [ ] New tests in `Curl.Engine.UnitTests` (a new file) prove that when a handler returns a report without `Timings`, the engine path gives it timings with a non-zero total time and the connect time of the first recorded connection, and gives it the upload size the transfer sent.
- [ ] A test proves a handler that already stamped its own `Timings` (for example SMB or HTTP) is not overwritten (no regression).
- [ ] A test proves a failed transfer (non-zero exit) still reports its timings and the bytes sent.
- [ ] A test in one extension handler's `.UnitTests` (redis) proves the bytes of a `-d` body appear as the upload size through the engine path.
- [ ] Tests are platform-neutral and use a fake `TimeProvider`.
- [ ] Hand check recorded in the Log: the redis command prints `time_total` greater than 0 and `up=3` or more; repeat for kafka, amqp1, mqtt5 and nats where a broker is at hand.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` is narrowed from the whole Engine library to the decorator that wraps every handler; if the upload size needs a count from the connection, add the one connector file in `Curl.Engine.UnitLibrary` that holds it.
- Review acceptance check: the redis command prints `time_total` > 0 and `up=3` or more.

## Log

- 2026-11-17: Created.
