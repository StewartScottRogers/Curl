---
id: BL-3561
title: Fix CGR-032: RST_STREAM HTTP_1_1_REQUIRED (0xd) does not fall back to HTTP/1.1; REF reconnects over HTTP/1.1 and succeeds
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Protocol.Http.UnitLibrary/HttpConnectionInfoLines.cs, Curl.Protocol.Http.UnitTests/Cgr032Tests.cs, Record-CurlExchange.ps1]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3561 — Fix CGR-032: RST_STREAM HTTP_1_1_REQUIRED (0xd) does not fall back to HTTP/1.1; REF reconnects over HTTP/1.1 and succeeds

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-032 (severity High): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-032: RST_STREAM HTTP_1_1_REQUIRED (0xd) does not fall back to HTTP/1.1; REF reconnects over HTTP/1.1 and succeeds

- **Severity:** High (IIS and other servers send this when they need TLS renegotiation or client certificates)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_rst13`)
- **Confidence:** Probable. Detail: Probable (the fallback is curl logic and was checked only against 8.18)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-6`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `rst13`: it answers every HTTP/2 request with RST_STREAM code 13 and serves HTTP/1.1 normally.
- Commands:
  - `curl -sS --http2-prior-knowledge http://127.0.0.1:P/x`
  - `curl -sSk https://127.0.0.1:P/x`
  - `curl -sSk -d hello https://127.0.0.1:P/x`
- Cases: `r_rst13`, `x_rst13_tls`, `x_rst13_tls_post`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge http://127.0.0.1:P/x
curl -sSk https://127.0.0.1:P/x
curl -sSk -d hello https://127.0.0.1:P/x
```

**Expected (real curl)**

- reconnects and sends `GET /x HTTP/1.1` (for the POST case, the body as well). It exits 0 with `h1 hello`, and `-w` gives `[1.1 200 2]`.

**Actual (Curl)**

- exit 92, `curl: (92) HTTP/2 stream 1 was not closed cleanly: HTTP_1_1_REQUIRED (err 13)`, `[0 000 1]`. No second connection is made.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs:375`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr032Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Touches widened (no task in Doing on origin/work/dark-factory names any of them): the retry lives in `HttpProtocolHandler.cs` (`FailedOutcome`, `Http11RequiredOutcome`, `HttpRequestPlan.AfterHttp11Required`/`DowngradedToHttp11`, `TargetOf` ALPN), the flag in `HttpTransferException.cs`, the `-v` line in `HttpConnectionInfoLines.cs`, and the measurement needed `Record-CurlExchange.ps1 -Http2ResetCode` (RST_STREAM with a code, every connection served on its own thread, since curl keeps the first open while it retries).
- Decision (ADR-0819): a reset with HTTP_1_1_REQUIRED is resent once over HTTP/1.1 on a new connection whose ALPN offers only `http/1.1`; `-v` says `Downgrades to HTTP/1.1`; a downgraded plan is never downgraded again.
- No curl 8.22.0 with HTTP/2 is on this machine (Git for Windows' 8.22.0 Schannel has no nghttp2), so REF is WSL curl 8.18.0 / nghttp2 1.68 against the recorder on 172.26.96.1; PORT-WIN is Curl.Console's Debug curl.exe against the recorder on 127.0.0.1. Response `HTTP/1.1 200 OK\r\nContent-Length: 9\r\n\r\nh1 hello\n`, each command with `-w '[%{http_version} %{http_code} %{num_connects}]'`:
  - `--http2-prior-knowledge -sS http://H:P/x`: REF exit 0, `h1 hello\n[1.1 200 2]`, stderr empty; conn 1 HEADERS GET -> RST 13, conn 2 `GET /x HTTP/1.1`. PORT identical.
  - `-sSk https://H:P/x`: REF (Linux offers h2 by default) exit 0, same stdout, conn 1 ALPN h2, conn 2 ALPN http/1.1 `GET /x HTTP/1.1`. PORT with `--http2` (Windows offers only http/1.1 by default, as Schannel curl) identical; PORT without `--http2` exit 0 `[1.1 200 1]`, one HTTP/1.1 connection, as the Windows REF has no HTTP/2.
  - `-sSk -d hello https://H:P/x`: REF exit 0, same stdout, conn 2 `POST /x HTTP/1.1` with `Content-Length: 5` and the body. PORT (`--http2`) identical.
  - Only difference: curl sends conn 1's GOAWAY when it exits, Curl when it closes conn 1 before connecting again; each connection's bytes are the same (ADR-0819 Consequences).
- Before the fix (the downgrade branch disabled) the two `Cgr032Tests` retry tests fail; with it all three pass. Variants that already matched (another RST code: exit 92, one connection; REFUSED_STREAM, Cgr031Tests) still pass.
- Fast tests: 46 test projects passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members. Linux and macOS: the tests use only scripted connections and no paths, so they are platform-neutral.
## Log

- 2026-10-26: Created from CGR-032 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. An HTTP/2 stream reset with HTTP_1_1_REQUIRED is resent once over HTTP/1.1 on a new connection, as curl does
