---
id: BL-3597
title: Fix CGR-068: -H 'Transfer-Encoding: chunked' on a request with no body (GET, HEAD) sends no terminating 0 chunk
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs, Curl.Protocol.Http.UnitTests/Cgr068Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3597 — Fix CGR-068: -H 'Transfer-Encoding: chunked' on a request with no body (GET, HEAD) sends no terminating 0 chunk

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-068 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-068: -H 'Transfer-Encoding: chunked' on a request with no body (GET, HEAD) sends no terminating 0 chunk

- **Severity:** Medium
- **Area:** http-requests
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: Confirmed: reproduced 2/2 on Windows and 2/2 on Linux.
- **Related existing finding (GF/AF):** none
- **Source findings:** `http-requests HR-8`

**Reproduction**

- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- Run `curl -sS -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a` and `curl -sS -I -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a`, cases v8_*.

Commands (run each with REF and with PORT):

```sh
curl -sS -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a
curl -sS -I -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a
```

**Expected (real curl)**

- Sends the head with `Transfer-Encoding: chunked` and then `0\r\n\r\n`.

**Actual (Curl)**

- Sends the head with `Transfer-Encoding: chunked` and nothing after it. A conforming server then waits for a chunked body that never comes; the harness server only answered after its 1.5 s read timeout.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:236-238`. The no-body framing is built with chunked = false whatever the custom Transfer-Encoding header says.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr068Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured 2026-10-26 on Windows with `Record-CurlExchange.ps1` (port 20335, response `HTTP/1.1 200 OK`, `Content-Length: 3`, body `ok\n`), REF = Git for Windows ucrt64 curl 8.22.0, PORT = `Curl.Console\bin\Debug\net10.0\curl.exe`:
  - `curl -sS -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a`: REF sends `GET /a HTTP/1.1`, Host, User-Agent, Accept, `Transfer-Encoding: chunked`, the empty line, then `0\r\n\r\n`; exit 0, stdout `ok\n`, stderr empty. Before the fix PORT sent the head alone; after it request.bin, stdout.bin, stderr.txt and exitcode.txt are byte-identical to REF's (cmp).
  - `curl -sS -I -H "Transfer-Encoding: chunked" http://127.0.0.1:20335/a`: REF sends the same with `HEAD /a`, then `0\r\n\r\n`; exit 0, stdout the response head. After the fix all four files are byte-identical to REF's.
  - With `-v` both also match (bar REF's debug-build warning and `Curl_xfer_send` trace lines): `} [5 bytes data]` and `* upload completely sent off: 5 bytes` after the head. Neither command writes a file.
- Fix: `HttpRequestFraming.OfNoBody` gives a request with no body an empty chunked `BytesBody` (no Content-Type, Content-Length or Expect) when the first `Transfer-Encoding` `-H` value asks for `chunked`, so the existing chunked writer sends `0\r\n\r\n`. A bug fix matching measured curl with no design choice, so no ADR.
- `Cgr068Tests.cs`: the GET and HEAD tests failed with the fix disabled (2 failed, 1 passed) and pass with it; the third (`Transfer-Encoding: gzip`) pins that other codings still send the head alone.
- Variants that already matched: the issue lists none beyond the two commands; the existing chunked POST and upload tests stay green in the fast run.
- Linux and macOS: the change and its tests are platform-neutral (fake connection, no paths or OS text), and the issue's Linux REF sends the same `0\r\n\r\n`.
- `dotnet build`: 0 warnings, 0 errors. Fast tests green. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-26: Created from CGR-068 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A GET or HEAD with -H 'Transfer-Encoding: chunked' sends the terminating 0 chunk, byte-identical to curl 8.22.0
