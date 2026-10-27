---
id: BL-3652
title: Fix CGR-123: curl_ws_send sends PING/PONG/CLOSE control frames with payloads over 125 bytes; libcurl refuses them with CURLE_TOO_LARGE
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyWebSocket.cs, Curl.Engine.UnitTests/Cgr123Tests.cs, Curl.Protocol.Ws.UnitLibrary/WsFrameEncoder.cs, Curl.Protocol.Ws.UnitTests/Cgr123Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3652 — Fix CGR-123: curl_ws_send sends PING/PONG/CLOSE control frames with payloads over 125 bytes; libcurl refuses them with CURLE_TOO_LARGE

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-123 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-123: curl_ws_send sends PING/PONG/CLOSE control frames with payloads over 125 bytes; libcurl refuses them with CURLE_TOO_LARGE

- **Severity:** Medium
- **Area:** libcurl-api
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed (Windows, Linux).
- **Related existing finding (GF/AF):** none
- **Source findings:** `libcurl-api LA-3`

**Reproduction**

- *Setup (libcurl-api):* EV = `SCR\evidence\libcurl-api`. Build the probe: `cd EV\probe && dotnet build -c Release -o out`. It calls REF libcurl (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) through P/Invoke and the port's managed `Libcurl.dll` (`SCR\Curl\Libcurl\bin\Release\net10.0`) in the same process against the same loopback server. `EV\probe\out\probe.exe repro` starts an HTTP server on 127.0.0.1:21410 and prints REF and PORT side by side; other subcommands: `ws`, `sweep`, `multiids`, `cb`, `upload http10`, `form` (21411), `printf2`, `url1`, `lport`, `state`. Linux: `dotnet publish -c Release -r linux-x64 --self-contained -o EV\verify\outlinux`, then `EV\verify\linux_verify.sh`.
- run `probe.exe ws send-ping-126 send-close-200 send-pong-126`. It uses a WebSocket server on 127.0.0.1:21407 with CONNECT_ONLY=2, then calls `curl_ws_send(h, buf, 126, &sent, 0, CURLWS_PING)`, the same with a 200-byte CURLWS_CLOSE, and a 126-byte CURLWS_PONG.

Commands (run each with REF and with PORT):

```sh
probe.exe ws send-ping-126 send-close-200 send-pong-126
```

**Expected (real curl)**

- returns 100 (CURLE_TOO_LARGE) with sent=0, and nothing reaches the wire.

**Actual (Curl)**

- returns 0 with sent=126 (or 200). The server receives `fin=1 op=9/8/10 len=126/200`, an illegal control frame under RFC 6455 section 5.5.

**Suspected location in source**

- `Curl.Protocol.Ws.UnitLibrary\WsFrameEncoder.cs` has no 125-byte limit for control opcodes. The call goes through `Curl.Engine.UnitLibrary\ManagedEasyWebSocket.cs`.

**Acceptance criterion:** Running the probe case(s) in the repro gives PORT return codes, callback sequences, CURLINFO values and bytes on the wire equal to REF's on Windows and REF-LINUX's on Linux (Expected).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr123Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26 (REF): a throwaway C# file-based probe (not committed; the report's `SCR` probe is gone) calling libcurl 8.22.0 Schannel (`C:\Program Files\Git\ucrt64\bin\libcurl-4.dll`) over P/Invoke against a loopback WebSocket server, `CURLOPT_CONNECT_ONLY` 2, then `curl_ws_send` of a zeroed buffer: ping 126 bytes (`CURLWS_PING`) rc=100 sent=0; close 200 rc=100 sent=0; pong 126 rc=100 sent=0; ping 125 rc=0 sent=125. The server received exactly one frame, `fin=1 op=9 len=125`.
- PORT before the fix: rc=0 with sent=126/200/126 and the frames on the wire (`Curl.Engine.UnitTests/Cgr123Tests.cs` failed 5 of its 6 cases with the production change reverted). PORT after: the same tests pin REF's answers (`CurlCode.TooLarge`, sent 0, nothing written; the 125-byte ping goes out), so PORT equals REF. The test drives `ManagedEasyWebSocket`, which Libcurl's `curl_ws_send` calls directly.
- Fix: `WsFrameEncoder.IsControlPayloadTooLong` (`LongestControlPayloadLength` 125, RFC 6455 section 5.5); `ManagedEasyWebSocket.StartFrame` refuses with `TooLarge` through the new `FrameRefusalOf` (split out to keep `StartFrame` under the complexity gate: it measured 12 with the check inline). Both `curl_ws_send` (under `CURLWS_OFFSET` by the announced size too) and connect-only `curl_ws_start_frame` refuse. The read-callback upload's `StartUploadFrame` path (`WsUploadFrames`, not in touches, not in the repro) is unchanged.
- Variants that already matched still match: data frames of any size and control frames of up to 125 bytes (`Cgr123Tests` in both projects; every existing `ManagedEasyWebSocketTests` case passes).
- No ADR: there was no design choice beyond matching measured curl.
- Gates: `dotnet build` clean; all fast tests passed (exit 0); `Measure-CodeQuality.ps1` for `Curl.Engine.UnitLibrary` and `Curl.Protocol.Ws.UnitLibrary` 100% line and branch, no failing member. Platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS sees the same results.

## Log

- 2026-10-26: Created from CGR-123 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. curl_ws_send and curl_ws_start_frame refuse PING/PONG/CLOSE payloads over 125 bytes with CURLE_TOO_LARGE, sent 0, nothing on the wire, as curl 8.22.0
