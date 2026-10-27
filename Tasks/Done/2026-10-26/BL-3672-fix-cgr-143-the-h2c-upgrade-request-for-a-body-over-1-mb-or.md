---
id: BL-3672
title: Fix CGR-143: The h2c Upgrade request for a body over 1 MB, or for a chunked -T - upload, carries Expect: 100-continue; REF sends no Expect on an Upgra...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitTests/Cgr143Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3672 — Fix CGR-143: The h2c Upgrade request for a body over 1 MB, or for a chunked -T - upload, carries Expect: 100-continue; REF sends no Expect on an Upgra...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-143 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-143: The h2c Upgrade request for a body over 1 MB, or for a chunked -T - upload, carries `Expect: 100-continue`; REF sends no Expect on an Upgrade request

- **Severity:** Medium
- **Area:** http2-linux
- **Platform(s):** Linux. Detail: Linux verified. The request is built by shared code, so Windows is presumed to behave the same.
- **Confidence:** Probable
- **Related existing finding (GF/AF):** GF-0025, GF-0095
- **Source findings:** `http2-linux H2L-13`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server `--mode auto` (accepts the Upgrade).
- Commands:
  - `curl -sS --http2 --data-binary @2m.bin http://127.0.0.1:P/echo` (`2m.bin` = 2 MiB)
  - `curl -sS --http2 -T - http://127.0.0.1:P/echo < /dev/null`
- Cases: `m_expect2m_up`, `u_T_stdin`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2 --data-binary @2m.bin http://127.0.0.1:P/echo
curl -sS --http2 -T - http://127.0.0.1:P/echo < /dev/null
```

**Expected (real curl)**

- sends `POST /echo HTTP/1.1 ... Upgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\nContent-Length: 2097152\r\nContent-Type: application/x-www-form-urlencoded\r\nConnection: Upgrade, HTTP2-Settings\r\n\r\n`, with no Expect. For `-T -` it exits 16 with `Error in the HTTP2 framing layer`, `[1.1 101 1]`.

**Actual (Curl)**

- sends the same bytes plus `Expect: 100-continue\r\n` before `Connection:`. For `-T -` it exits 56 with `curl: (56) Recv failure: Broken pipe`, `[0 000 1]`.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\HttpRequestHeadFormatter.cs` / `HttpRequestFraming.cs` (Expect is not suppressed on the h2c-upgrade head)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2) (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr143Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 3): Added `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` to touches: the wait for `100 Continue` is decided by the plan's framing in the handler, not the head formatter, and the h2c decision is only known there (the exchange's `HttpH2cUpgradeConnection`). No other task in Doing named it.
- Fix: `HttpRequestFraming.ForH2cUpgrade` drops curl's own `Expect: 100-continue` line and its wait; chunking stays and an `-H Expect: 100-continue` still waits. `HttpProtocolHandler.ExchangeAsync` uses it when the exchange runs on an h2c upgrade connection (`PlanForConnection`). No ADR: this matches measured curl and takes no design choice.
- Measured REF-LINUX (WSL curl 8.18.0, nghttp2 1.68.0) against `nc -l`: `curl -sS --http2 --data-binary @2m.bin http://127.0.0.1:P/echo` sends `POST /echo HTTP/1.1`, Host, User-Agent, `Accept: */*`, `Upgrade: h2c`, `HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA`, `Content-Length: 2097152`, `Content-Type: application/x-www-form-urlencoded`, `Connection: Upgrade, HTTP2-Settings`, then the body at once, with no Expect. `curl -sS --http2 -T - http://127.0.0.1:P/echo < /dev/null` sends `PUT /echo HTTP/1.1`, Host, User-Agent, Accept, `Transfer-Encoding: chunked`, Upgrade, HTTP2-Settings, Connection, then `0\r\n\r\n` at once.
- End to end against a PowerShell TcpListener h2c server (it reads the head and any Content-Length body, answers `HTTP/1.1 101`, an empty SETTINGS, a SETTINGS ACK and HEADERS `:status 200` END_STREAM on stream 1, then logs everything until the close). REF-LINUX ran from WSL against the Windows host; Curl ran via `dotnet run --project Curl.Console`. Both commands, with `-w "[%{http_version} %{http_code} %{num_connects}]"`: REF exit 0, stdout `[2 200 1]`, stderr empty; Curl exit 0, stdout `[2 200 1]`, stderr empty. Both write no files. With Host and User-Agent normalised, the bytes on the wire are identical: 311 bytes for `-T -`, and 2,097,505 bytes for the 2 MiB POST (HTTP/1.1 head and body, then the HTTP/2 preface and frames).
- The report's exit 16 (`Error in the HTTP2 framing layer`, `[1.1 101 1]`) for `-T -` came from its scripted nghttp2 server's answer, not from curl. Against the server above, real curl 8.18.0 exits 0, and Curl now sends it the same bytes and does the same. Before the fix, Curl's Expect wait was what the server answered differently (exit 56 in the report). Curl 8.22.0 has no HTTP/2 on Windows, so REF-LINUX 8.18.0 is the curl measured here.
- Repro variants that already matched still match: the POST's head and the `-T -` head are otherwise unchanged (`HttpRequestHeadFormatterTests.H2cUpgrade` and `HttpProtocolHandlerTests.H2cUpgrade` pass), and an Upgrade request with a body under 1 MiB never carried Expect.
- Found while measuring: a server that answers the Upgrade before reading the body gets 589,898 bytes from REF and the whole 2 MiB body from Curl. That case differs from this issue's, so it is filed as BL-3910.
- Unit tests `Cgr143Tests` (2) both failed with the fix turned off (the Expect line in the head) and pass with it on. Fast tests: 46 projects green, no failures (Curl.Protocol.Http.UnitTests 2437 passed, 19 skipped). `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. The change is platform-neutral (no paths or OS text), which covers CI on Linux and macOS.

## Log

- 2026-10-26: Created from CGR-143 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. An h2c Upgrade request sends no Expect: 100-continue of curl's own and sends its body at once, as curl does
