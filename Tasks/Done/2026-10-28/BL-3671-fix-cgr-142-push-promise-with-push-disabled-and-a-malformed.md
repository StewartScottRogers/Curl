---
id: BL-3671
title: Fix CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667, BL-3912]
touches: [Curl.Http2.UnitLibrary/Http2Connection.cs, Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Http2.UnitTests/Cgr142Tests.cs, Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitTests/Cgr142Tests.cs, Curl.Http2.UnitLibrary/Http2ProtocolException.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http2.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3671 — Fix CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-142 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-142: PUSH_PROMISE (with push disabled) and a malformed or missing :status give different exit codes

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_push`, `r_statusbad`, `r_nostatus`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-12`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens: `push`, `statusbad` (`:status 2x0`), `statusspace`, `statusshort`, `nostatus`.
- Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`
- Cases: `r_push`, `r_statusbad`, `r_nostatus` (the tester also ran `r2_statusspace`, `r2_statusshort`, `x_push_tls` and `x_nostatus_tls`).

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
```

**Expected (real curl)**

- push: exit 92, `curl: (92) [HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=60, hend=1], error -505: Protocol error`.
- Bad :status: exit 16, `Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]`.
- Missing :status: exit 8, `curl: (8) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.

**Actual (Curl)**

- push: exit 16, `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`.
- Bad or missing :status: exit 92, `curl: (92) HTTP/2 stream 1 was not closed cleanly: PROTOCOL_ERROR (err 1)`. PORT sends RST_STREAM instead of GOAWAY.

**Suspected location in source**

- `Curl.Http2.UnitLibrary\Http2Connection.cs:581` (`ReceivePushPromise`); `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:23` (`StatusOf`)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr142Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 9, first pass before any edit): all three exit codes are chosen in `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs`. Its `FailureOf` maps `Http2ProtocolException` (thrown by `Http2Connection.ReceivePushPromise` when push is disabled) to exit 16 "nghttp2 shuts down connection", and `ResetMalformedAsync` (reached from `Http2ResponseHead.StatusOf` returning null) sends RST_STREAM and gives exit 92 "not closed cleanly". The fix: a push-disabled PUSH_PROMISE gives exit 92 with the "[HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=N, hend=1], error -505: Protocol error" text; a :status that is present but not three digits gives exit 16 "Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]"; a missing :status sends GOAWAY PROTOCOL_ERROR and gives exit 8 "nghttp2 shuts down connection with error 1: PROTOCOL_ERROR". `StatusOf` must then tell "missing" from "invalid". New message text goes in `HttpTransferMessages.cs`. Added `Http2StreamConnection.cs` and `HttpTransferMessages.cs` to touches for that.
- `Http2StreamConnection.cs` is held by BL-3667 (in Doing on work/dark-factory), so `depends-on` names BL-3667.
- Measuring real curl needs an HTTP/2 server that sends PUSH_PROMISE, a bad :status, or no :status. `Record-CurlExchange.ps1 -Http2Origin` cannot yet do that, and the local curl 8.22.0 is a Schannel build with no nghttp2. Filed BL-3912 to extend the script and record curl.se's nghttp2 build of 8.22.0 (see BL-658 Notes); this task depends on it and pins text from its Notes.
- 2026-10-28 (lane 8): curl 8.22.0 (BL-3912 Notes) differs from the report's 8.18.0 for :status: a :status that is not three digits is exit 92 `Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]` (not 16), and a missing :status is exit 8 `[HTTP2] [1] received invalid frame: FRAME[HEADERS, len=4, hend=1, eos=1], error -532: Violation in HTTP messaging rule`, both after RST_STREAM PROTOCOL_ERROR and then the closing GOAWAY `shutdown`. The criterion says to re-check against 8.22.0, so 8.22.0 is what is pinned.
- Fix: `Http2ProtocolException` gained `GoAwayDebugData` and `InvalidFrame` (init properties); `Http2Connection.ReceivePushPromise` with push off sets both, so the GOAWAY carries `PUSH_PROMISE: push disabled` (no NUL, as nghttp2's reason string) and `Http2StreamConnection.ConnectionErrorOf` reports exit 92 naming the frame (len = the frame's payload length, hend from its flags). `Http2StreamConnection.RejectHeadWithoutValidStatusAsync` resets the stream and gives exit 92 naming the :status field, or exit 8 naming the HEADERS frame when there is none. `Http2ResponseHead.cs` did not need changing. Added `Http2ProtocolException.cs` and `HttpProtocolHandlerTests.Http2.cs` to touches (no other Doing task on work/dark-factory names either): the latter held `ExecuteAsync_Http2HeadWithoutAValidStatus_ResetsTheStreamAndFailsWithExit92`, which pinned the old text and is replaced by the Cgr142Tests cases.
- Default taken: the HEADERS frame's `len` is the length of the whole header block Curl hands the stream (CONTINUATIONs joined, padding and priority removed); nghttp2 prints the frame's payload length. They agree for the measured case (a single unpadded HEADERS); the difference only shows for a padded or continued head with no :status.
- Repros, `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x` via `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2Answer <answer>`, Curl = `Curl.Console/bin/Debug/net10.0/curl.exe`, real curl = BL-3912's fixtures (curl.se 8.22.0_2 mingw, nghttp2 1.70.0). Stdout empty and no files written in every case for both; request bytes equal except the three Huffman-coded bytes of the port in `:authority` (different listen ports, normalised):
  - `push`: both exit 92 `curl: (92) [HTTP2] [1] received invalid frame: FRAME[PUSH_PROMISE, len=7, hend=1], error -505: Protocol error`; both send GOAWAY last stream 0 error 1 debug `PUSH_PROMISE: push disabled` and nothing after. (Before: exit 16 `nghttp2 shuts down connection`, empty debug data.)
  - `status:2x0`, `status:20`, `status:200 `: both exit 92 `curl: (92) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [:status], value: [2x0]` (`[20]`, `[200 ]`); both send RST_STREAM 1 error 1 then GOAWAY error 0 `shutdown`. (Before: exit 92 `HTTP/2 stream 1 was not closed cleanly`.)
  - `nostatus`: both exit 8 `curl: (8) [HTTP2] [1] received invalid frame: FRAME[HEADERS, len=4, hend=1, eos=1], error -532: Violation in HTTP messaging rule`; both RST_STREAM 1 error 1 then GOAWAY `shutdown`. (Before: exit 92 not closed cleanly.)
- Focused tests: `Curl.Http2.UnitTests/Cgr142Tests.cs` (GOAWAY bytes with the debug data and `InvalidFrame`; another connection error keeps empty debug data) and `Curl.Protocol.Http.UnitTests/Cgr142Tests.cs` (each case's exit, message, wire and empty output, plus hend=0). Each asserts text or bytes the code before the fix did not produce (old exit 16 / "not closed cleanly" / empty GOAWAY debug data), so they fail before the fix.
- Variants that already matched (DATA before the head still exit 92 not closed cleanly, content-length errors exit 16, other connection errors exit 16 with empty GOAWAY debug data) are unchanged; the whole fast suite passes. Changes are platform-neutral (no paths, OS text or certificates), which covers CI on Linux and macOS.
- Measure-CodeQuality.ps1 -Library Curl.Http2.UnitLibrary,Curl.Protocol.Http.UnitLibrary: Curl.Http2.UnitLibrary 100/100, no failing member; Curl.Protocol.Http.UnitLibrary had one: the hend=0 branch of `Http2PushPromiseReceivedWithPushDisabled`, covered since by `ExecuteAsync_Http2PushPromiseWithoutEndHeaders_NamesTheFrameWithHend0`. Worst CRAP 10.

## Log

- 2026-10-26: Created from CGR-142 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits for BL-3667 (holds Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs) and BL-3912 (Record-CurlExchange HTTP/2 push and :status scenarios to measure curl)
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. PUSH_PROMISE with push off, an invalid :status and a missing :status give curl 8.22.0's exit codes, messages and frames
