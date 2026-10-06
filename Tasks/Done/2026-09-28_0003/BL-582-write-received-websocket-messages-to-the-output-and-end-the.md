---
id: BL-582
title: Write received WebSocket messages to the output and end the transfer as curl does
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-581]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-582 — Write received WebSocket messages to the output and end the transfer as curl does

## Goal

The payload of each received data frame reaches the transfer's output exactly as the curl 8.21.0 tool writes it, whatever it does with standard input or `-d` on a WebSocket (per BL-579's ADR) is done, and the transfer ends (server close, connection loss, `-m`) with curl's exit code, message, `%{size_download}` and progress.

## Context

- Conformance audit 2026-09-28, row 36. Builds on BL-581. Post-upgrade behaviour: BL-579's ADR and its measurements.
- Measure any case BL-579 did not: two text messages then close, a fragmented binary message, the server dropping the connection without a close frame, and `-w '%{size_download} %{http_code}'`.

## Acceptance criteria

- [x] Measured first as above (where BL-579 did not); stdout bytes, stderr, exit code and `-w` values copied into Notes.
- [x] `Curl.Protocol.Ws.UnitTests` pin output bytes, progress reports and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- From BL-580 (measured 2026-09-28): a `101` followed at once by the server closing, with no
  frame at all, ends with exit 52 `Empty reply from server` (the `-D` head is still written),
  not 0. `WsProtocolHandler` currently returns success right after the `101`; the frame loop
  built here replaces that, and `WsUpgradeResponse.Remaining` holds bytes that arrived with
  the head.
- From BL-581: `WsFrameReceiver.ReceiveAsync(Remaining, writePayload, token)` is the frame
  loop, ready to call after the `101`. It decodes every frame (ADR-0131), writes nothing itself
  but hands each payload run to `writePayload`, answers pings, throws `WsTransferException`
  (56/55) on a violation or failed read or pong, and returns the frame bytes received (0 means
  exit 52). `WsFrameEncoder.Encode(WsOpcode.Binary, upload, randomSource)` makes the `-T` frame.
- Measured 2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`, `curl -sS -w
  '%{size_download} %{http_code} %{size_upload} %{size_header} %{size_request} %{size_delivered}'
  ws://127.0.0.1:<port>/`, every reply the 102-byte `101` head, request 192 bytes. stderr empty
  unless given:
  | Frames after the `101` | stdout (payload, then `-w`) | Exit |
  | --- | --- | --- |
  | `81 03 "one"` `81 03 "two"` `88 02 03 e8` | `onetwo` `03 e8` `14 101 0 102 192 8` | 0 |
  | `02 02 01 02` `80 01 03` `88 00` (fragmented binary) | `01 02 03` `9 101 0 102 192 3` | 0 |
  | `81 05 "hello"`, then the server drops the connection | `hello` `7 101 0 102 192 5` | 0 |
  | `81 05 "hel"` (cut short), then dropped | `hel` `5 101 0 102 192 3` | 0 |
  | none | `0 101 0 102 192 0`; stderr `curl: (52) Empty reply from server` | 52 |
  | `81 02 "ok"` with `-T -` and standard input `abc`, held 1.5 s | sent `82 83 <mask> <abc masked>`; `ok` `4 101 9 102 201 2` | 0 |
  | `89 02 "hi"` `81 02 "ok"`, held 1.5 s | pong sent; `ok` `8 101 0 102 200 2` | 0 |
  | `81 02 "ok"` with `-d xyz`, held 1.5 s | nothing sent after the request (192 bytes); `ok` `4 101 0 102 192 2` | 0 |
  | `81 02 "ok"` with `-m 1`, held 3 s | `ok` `4 101 0 102 192 2`; stderr `curl: (28) Operation timed out after 1009 milliseconds with 4 bytes received` | 28 |
- So: `%{size_download}` is frame bytes received, heads included; `%{size_upload}` is the `-T`
  frame; `%{size_request}` is the upgrade request plus the `-T` frame plus every pong;
  `%{size_header}` is the head; `%{http_code}` stays `101` on 52. `%{size_delivered}` is the
  payload bytes written, which `TransferReport` cannot carry until BL-516 lands: filed as BL-777.
- Built: `WsProtocolHandler.ExchangeFramesAsync` runs after a `101` - sends `-T` as one binary
  frame (`ReportUploaded(frame, frame)`), runs `WsFrameReceiver` with payloads written to
  `Output`, ends 0 when the server closes after at least one frame byte and 52 when none came,
  and keeps the report (sizes above, code 101) on every outcome, including a 56 or 23 failure.
  `ReportTransferStarted` is called once connected and `ReportTransferDone` after the frame loop;
  `WsFrameReceiver` now takes `ITransferProgress`, reports `ReportDownloaded(frame bytes, null)`
  after every read, and exposes `BytesReceived` and `BytesSent` (pongs) instead of returning a
  count. `-m` is the runner's (ADR-0117): the cancellation escapes, and the progress report gives
  its `with 4 bytes received`.
- Choices taken without a measurement (rule 1): a `-T` source whose read fails ends the upload
  and the bytes read so far are sent, as the HTTP library takes a failed file read; a failed
  write to the output fails with 23 and the same `Failure writing output to destination, passed
  N returned M` as `-D` (renamed `WsIoFailures.HeaderWriteFailed` to `WriteFailed`), since curl
  writes both through one client writer. Neither needed an ADR: they follow ADR-0128/0131 and
  the HTTP library's existing behaviour.
- Tests: 164 in `Curl.Protocol.Ws.UnitTests` (12 new). `Measure-CodeQuality.ps1 -Library
  Curl.Protocol.Ws.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. WebSocket payloads reach the output after a 101, -T goes as one binary frame, and the transfer ends 0/52/56/23/28 with curl's -w sizes
