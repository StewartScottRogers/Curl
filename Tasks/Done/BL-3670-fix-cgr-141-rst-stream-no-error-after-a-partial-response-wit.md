---
id: BL-3670
title: Fix CGR-141: RST_STREAM NO_ERROR after a partial response without content-length exits 92; REF ends the transfer cleanly with exit 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferMessages.cs, Curl.Protocol.Http.UnitTests/Cgr141Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3670 — Fix CGR-141: RST_STREAM NO_ERROR after a partial response without content-length exits 92; REF ends the transfer cleanly with exit 0

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-141 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-141: RST_STREAM NO_ERROR after a partial response without content-length exits 92; REF ends the transfer cleanly with exit 0

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_rstmid0`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-11`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `rstmid0`: HEADERS 200, DATA `partial` without END_STREAM, then RST_STREAM code 0. Command: `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`. Cases: `r_rstmid0`, `x_rstmid0_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
```

**Expected (real curl)**

- exit 0. The output is the headers and `partial`.

**Actual (Curl)**

- the same output, then exit 92 with `curl: (92) HTTP/2 stream 1 was not closed cleanly: NO_ERROR (err 0)`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs:375`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr141Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 2): Server: a throwaway C# file-based app in %TEMP%\bl3670 (BL-3669's h2c prior-knowledge server, adapted) that logs every frame the client sends. On stream 1, by `H2MODE`: `mid` = HEADERS 200 (no content-length), DATA `partial` without END_STREAM, RST_STREAM NO_ERROR (the issue's `rstmid0`); `cl` = the same with `content-length: 9`; `pre` = RST_STREAM NO_ERROR before any HEADERS. `cl` and `pre` are added variants to pin the cases next to the fix. Record-CurlExchange.ps1 was not extended: it has no scripted HTTP/2 server, and the server stayed out of the repository as BL-3669's did.
- REF: WSL curl 8.18.0 (OpenSSL, nghttp2 1.68). The Windows curl 8.22.0 here is built without HTTP/2, so 8.22.0 could not be re-checked. Command: `curl -sS --http2-prior-knowledge -i http://H:P/x`.
  - `mid`: exit 0, stdout `HTTP/2 200 \r\n\r\npartial`, stderr empty.
  - `cl`: exit 18, stdout `HTTP/2 200 \r\ncontent-length: 9\r\n\r\npartial`, stderr `curl: (18) end of response with 2 bytes missing`.
  - `pre`: exit 92, stdout empty, stderr `curl: (92) HTTP/2 stream 1 was closed cleanly, but before getting  all response header fields, treated as error` (two spaces after "getting", as curl prints it).
  - Wire for all three: preface, SETTINGS, WINDOW_UPDATE, HEADERS on 1, two WINDOW_UPDATEs on 1 after the reset, SETTINGS ack, GOAWAY `shutdown`.
- PORT before the fix: `mid` and `cl` both exit 92 `HTTP/2 stream 1 was not closed cleanly: NO_ERROR (err 0)`, with the same stdout as REF. `pre` gives exit 92 with the same "not closed cleanly" text. The wire matches REF in all three.
- PORT after the fix: exit code, stdout bytes, stderr text and the frame sequence equal REF in all three variants. No files are written. The only difference is the HEADERS length, 34 against 36 bytes, because `:authority` is 127.0.0.1 here and the WSL host address 172.26.96.1 for REF.
- Fix: in `Http2StreamConnection`, a RST_STREAM NO_ERROR that arrives after the final head ends the response like END_STREAM (`EndOnCleanReset`), on both the read path and the queued path. `HttpResponseBodyReader` then reports a body shorter than its content-length as exit 18, as it already does. A NO_ERROR reset before the final head gives curl's "closed cleanly, but before getting  all response header fields" message (`HttpTransferMessages.Http2StreamClosedBeforeHead`). Any other reset code keeps its path. No ADR: this matches measured curl and makes no design choice.
- touches widened to HttpTransferMessages.cs for the new message. No task in Doing on origin/work/dark-factory names it.
- Cgr141Tests: three handler tests (mid, cl, pre) and one session test for the queued-reset path. With the fix disabled, the three handler tests fail; with it, all four pass. Fast tests: 44244 passed, 0 failed. Measure-CodeQuality -Library Curl.Protocol.Http.UnitLibrary: 100% line and branch coverage, 0 failing members. The tests use scripted bytes with no paths, OS text or certificates, so they are platform-neutral, which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-141 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A RST_STREAM NO_ERROR after the HTTP/2 response head ends the body cleanly (exit 0, or 18 when short of content-length), as real curl does
