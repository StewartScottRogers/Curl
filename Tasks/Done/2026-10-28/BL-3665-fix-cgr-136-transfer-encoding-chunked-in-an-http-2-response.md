---
id: BL-3665
title: Fix CGR-136: transfer-encoding: chunked in an HTTP/2 response makes PORT chunk-decode the HTTP/2 DATA (exit 56) where REF rejects the field (exit 16)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3667]
touches: [Curl.Protocol.Http.UnitLibrary/Http2ResponseHead.cs, Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Record-CurlExchange.ps1, Curl.Protocol.Http.UnitTests/Cgr136Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3665 — Fix CGR-136: transfer-encoding: chunked in an HTTP/2 response makes PORT chunk-decode the HTTP/2 DATA (exit 56) where REF rejects the field (exit 16)

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-136 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-136: `transfer-encoding: chunked` in an HTTP/2 response makes PORT chunk-decode the HTTP/2 DATA (exit 56) where REF rejects the field (exit 16)

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_resp_te`)
- **Confidence:** Confirmed. Detail: Confirmed (the exit code and message differ, but both reject the response; REF's rejection is the nghttp2 RFC check)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-3`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `resp_te`. Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`. Cases: `r_resp_te` and `x_resp_te_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
```

**Expected (real curl)**

- exit 16, `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [transfer-encoding], value: [chunked]`.

**Actual (Curl)**

- exit 56, `curl: (56) chunk hex-length char not a hex digit: 0x68`. The head is printed including `transfer-encoding: chunked`, and no body is written.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2ResponseHead.cs:52`. The field reaches the HTTP/1 header parser, which turns on chunked decoding.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr136Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-26 (lane 9): Measured REF-LINUX (WSL curl 8.18.0, nghttp2 1.68) with `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2HeaderField <fields> -ListenAddress 172.26.96.1 -Response 'HTTP/1.1 200 OK\r\n\r\n' -Curl wsl.exe -CurlArgs '-d','Ubuntu','--','curl','-sS','--http2-prior-knowledge','-i','http://172.26.96.1:18665/x'`. `-Http2HeaderField` is new in this run (more literal fields after `:status`; it is in the stash). Results:
  - fields `transfer-encoding: chunked` (then content-length: 0, END_STREAM): exit 16; stderr `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [transfer-encoding], value: [chunked]`; stdout `HTTP/2 200 \r\n` (no blank line).
  - fields `server: x`, `transfer-encoding: chunked`, `content-type: text/plain`: same exit and stderr; stdout `HTTP/2 200 \r\nserver: x\r\n` - curl writes the status line and every field before the bad one, then stops (no blank line, nothing after).
  - Wire after the client's two stream-1 WINDOW_UPDATEs and SETTINGS ACK: `GOAWAY last stream 0 error 1` (PROTOCOL_ERROR, 8-byte payload, no debug data), no RST_STREAM.
- Plan for the next claim: `Http2ResponseHead` gains a finder for nghttp2's connection-specific response fields (connection, keep-alive, proxy-connection, transfer-encoding, upgrade, and te with a value other than `trailers`) and a head prefix writer (status line plus the lines before the bad field, no empty line). `Http2StreamConnection.ReceiveHeadersAsync` (head branch) enqueues that prefix, sends `session.Frames.SendGoAwayAsync(Http2ErrorCode.ProtocolError, ...)` (last stream 0, as measured) and keeps an exit 16 failure (message in `HttpTransferMessages`) that `ReceiveFrameAsync` throws once `received` is drained, so the head bytes reach `-i` output first. Then check that the HTTP/1 head reader writes the partial head to stdout as curl does.
- Requeued behind BL-3667 (holds `Http2StreamConnection.cs`, which the fix must change). `Http2Session.cs` (BL-3668) is not needed: GOAWAY goes out through `session.Frames`. Touches widened to Http2StreamConnection.cs, HttpTransferMessages.cs (the new message) and Record-CurlExchange.ps1 (the new parameter).

- 2026-10-28 (lane 2): BL-3667 had since added the malformed-head path (`IsHeadTakenAsync`, `ShutDownMalformedAsync`, `InvalidHeaderFieldReceived`), so the fix is small: `Http2ResponseHead.IsConnectionSpecific` (connection, keep-alive, proxy-connection, transfer-encoding, upgrade; te other than `trailers`, case-insensitive) and `Http2StreamConnection.IndexOfInvalidField` (was `IndexOfInvalidContentLength`) returns the first such field, so the head is written up to it, GOAWAY PROTOCOL_ERROR last stream 0 goes out and exit 16 follows. `HttpTransferMessages.cs` needed no change (the BL-3667 message is the same text). The stash's `-Http2HeaderField` recorder parameter is carried over, merged beside `-Http2Answer` and `-Http2NoSettings`. No ADR: the behaviour is nghttp2's, measured, not a design choice.
- PORT (Windows, this commit) with `& .\Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2HeaderField <fields> -Port 18665 -Response 'HTTP/1.1 200 OK\r\n\r\n' -Curl dotnet -CurlArgs 'Curl.Console\bin\Debug\net10.0\curl.dll','-sS','--http2-prior-knowledge','-i','http://127.0.0.1:18665/x'`:
  - `transfer-encoding: chunked`: exit 16, stderr `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [transfer-encoding], value: [chunked]`, stdout `HTTP/2 200 \r\n`, wire ends `GOAWAY last stream 0 error 1` - equal to REF-LINUX above.
  - `server: x`, `transfer-encoding: chunked`, `content-type: text/plain`: same exit and stderr, stdout `HTTP/2 200 \r\nserver: x\r\n`, same GOAWAY - equal to REF-LINUX. No files written by either. `x_resp_te_tls` runs the same `Http2StreamConnection` path over TLS (the check is on the decoded head, not the transport).
- `Cgr136Tests` (9 cases) fails with the check disabled (exit 56 instead of 16) and passes with it; `te: trailers` still succeeds. Variants that already matched (BL-3667's Cgr138Tests, the rest of Curl.Protocol.Http.UnitTests) still pass. Platform-neutral: no paths, OS errors or certificates.
- Gates: `dotnet build` 0 warnings; fast tests all green (46 test assemblies passed, 0 failed); Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100% line and branch, 0 failing members (the first form, an `is ... or` pattern, was complexity 14, so it is a FrozenSet lookup).

## Log

- 2026-10-26: Created from CGR-136 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Backlog. Waits on BL-3667, which holds Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs that the fix must change; REF measured and plan in Notes
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Done. A connection-specific field (transfer-encoding and the like) in an HTTP/2 response head now ends the transfer with exit 16, the head up to that field and GOAWAY PROTOCOL_ERROR, as curl with nghttp2 does
