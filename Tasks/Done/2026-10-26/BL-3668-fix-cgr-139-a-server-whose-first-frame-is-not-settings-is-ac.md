---
id: BL-3668
title: Fix CGR-139: A server whose first frame is not SETTINGS is accepted; REF exits 16
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Protocol.Http.UnitTests/Cgr139Tests.cs, Curl.Protocol.Http.UnitTests/Http2SessionTests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http2Trace.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3668 — Fix CGR-139: A server whose first frame is not SETTINGS is accepted; REF exits 16

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-139 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-139: A server whose first frame is not SETTINGS is accepted; REF exits 16

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_nosettings`)
- **Confidence:** Confirmed. Detail: Confirmed (RFC 9113 3.4 preface check in nghttp2)
- **Related existing finding (GF/AF):** AF-0046
- **Source findings:** `http2-linux H2L-8`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `nosettings`, which sends HEADERS and DATA but never SETTINGS. Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`. Cases: `r_nosettings`, `x_nosettings_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
```

**Expected (real curl)**

- exit 16, `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.` REF sends GOAWAY `SETTINGS expected`.

**Actual (Curl)**

- exit 0. The full response and body are printed.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2Session.cs:400-430` (the first-frame check only covers a FRAME_SIZE_ERROR before SETTINGS)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr139Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Fix: `Http2Session.RejectUnlessSettingsCameFirstAsync` runs after every frame read while the server's SETTINGS has not arrived. A frame that is not a (non-ACK) SETTINGS - HEADERS, DATA, PING, or a SETTINGS ACK, as nghttp2's preface check counts it - fails the connection with exit 16 and `HttpTransferMessages.Http2SettingsExpected`, after sending GOAWAY last stream 0, PROTOCOL_ERROR, debug data `SETTINGS expected` (an IOException on that write is swallowed; the exit stays 16). A peer that closes before any frame is left to the existing close handling. No ADR: this only matches measured curl, no design choice.
- `Record-CurlExchange.ps1` gained `-Http2NoSettings` (the origin never sends SETTINGS) and now notes a GOAWAY's debug data. Added to touches with the two existing test files below: no task in Doing on origin/work/dark-factory names any of them.
- Three older tests scripted servers that never sent SETTINGS (two `Http2SessionTests` ReceiveAsync tests, `ExecuteAsync_H2cUpgrade401TracingFrames_WritesThePostUpgradeSettingsBeforeStream3`); they now send SETTINGS first, and the two ReceiveAsync tests consume it first so they still cover the branches they were written for (Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100% line, 100% branch, 0 failing members).
- Focused tests: `Cgr139Tests` (HEADERS first; SETTINGS ACK then HEADERS; GOAWAY write reset; SETTINGS first still reads). With the call disabled 3 of 4 fail; with it all pass.
- Measured, REF = WSL curl 8.18.0 (nghttp2 1.68.0), PORT = Curl.Console on Windows:
  - `Record-CurlExchange.ps1 -Http2Origin prior-knowledge -Http2NoSettings -Response 'HTTP/1.1 200 OK\r\n\r\n'` with `curl -sS --http2-prior-knowledge -i http://HOST:P/x` (r_nosettings). REF: exit 16, stdout empty, stderr `curl: (16) Remote peer returned unexpected data while we expected SETTINGS frame.  Perhaps, peer does not support HTTP/2 properly.`; wire: preface, SETTINGS 3=100 4=65536 2=0, WINDOW_UPDATE 0 1048510465, HEADERS 1 (GET /x), then after the server's HEADERS 1: WINDOW_UPDATE 1 twice, GOAWAY last 0 error 1 debug `SETTINGS expected`. PORT: the same exit, stdout, stderr and frames. Before the fix PORT exited 0 and printed the response.
  - `-Tls -Http2Origin alpn -ProxyAlpn h2 -Http2NoSettings` with `curl -sS -k --http2 -i https://HOST:P/x` (x_nosettings_tls): REF and PORT both exit 16, stdout empty, same stderr, same frames ending in GOAWAY `SETTINGS expected`.
  - Only difference: stderr ends CRLF on Windows (the console newline, as for every message), LF on Linux.
- No repro variant in the issue matched before; the server-sends-SETTINGS path is pinned by `ReadAsync_ServerSendsSettingsFirst_ReadsTheResponse` and the existing HTTP/2 tests, all green (46 fast test projects pass).
- Platform-neutral: the tests use scripted in-memory connections only - no paths, sockets or OS text - so CI on Linux and macOS sees the same; that criterion is met by this and the green run here.

## Log

- 2026-10-26: Created from CGR-139 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A server whose first HTTP/2 frame is not SETTINGS now fails with exit 16 and GOAWAY 'SETTINGS expected', as curl does
