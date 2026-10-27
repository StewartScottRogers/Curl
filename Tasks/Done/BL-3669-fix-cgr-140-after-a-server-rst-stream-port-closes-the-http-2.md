---
id: BL-3669
title: Fix CGR-140: After a server RST_STREAM, PORT closes the HTTP/2 connection and reconnects for the next URL, where REF keeps multiplexing on it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpTransferException.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr140Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3669 — Fix CGR-140: After a server RST_STREAM, PORT closes the HTTP/2 connection and reconnects for the next URL, where REF keeps multiplexing on it

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-140 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-140: After a server RST_STREAM, PORT closes the HTTP/2 connection and reconnects for the next URL, where REF keeps multiplexing on it

- **Severity:** Medium (with a server that resets only one stream, URL 2 fails in PORT where REF succeeds, and the overall exit codes differ: 92 vs 0)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\m_rst2_reuse`)
- **Confidence:** Probable
- **Related existing finding (GF/AF):** AF-0047
- **Source findings:** `http2-linux H2L-10`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scen `rst2sid1`: RST_STREAM INTERNAL_ERROR on stream 1 of each connection, normal answers on other streams.
- Command: `curl -sS --http2-prior-knowledge http://127.0.0.1:P/1 http://127.0.0.1:P/2 -w '[%{http_version} %{response_code} %{num_connects}]\n'`, plus the `-k https://` form.
- Cases: `m_rst2_reuse`, `m_rst2_reuse_tls`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge http://127.0.0.1:P/1 http://127.0.0.1:P/2 -w '[%{http_version} %{response_code} %{num_connects}]\n'
```

**Expected (real curl)**

- URL 1 fails with 92. URL 2 goes on stream 3 of the same connection: `hello h2`, `[2 200 0]`, overall exit 0.

**Actual (Curl)**

- URL 1 fails with 92. PORT then sends GOAWAY `shutdown` and closes. URL 2 opens connection 2 (stream 1 again), which is reset as well: `[0 000 1]`, overall exit 92.

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs` (the reset path leaves the session unusable)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr140Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- 2026-10-26 (lane 2): Server: a throwaway C# file-based app in %TEMP% (h2c prior knowledge, or TLS with ALPN h2 only): stream 1 of each connection gets RST_STREAM INTERNAL_ERROR, every other stream `200`, `content-length: 9`, `hello h2\n`. It logs every frame the client sends. Record-CurlExchange.ps1 could not be extended for this: another task in Doing holds it, so the server stayed out of the repository.
- REF: WSL curl 8.18.0 (OpenSSL, nghttp2 1.68), the only HTTP/2 curl here (the Windows reference has no HTTP/2). Commands: `curl -sS --http2-prior-knowledge http://H:P/1 http://H:P/2 -w '[%{http_version} %{response_code} %{num_connects}]\n'`, and the same with `-k --http2-prior-knowledge https://`. Plain `-k https://` gives the same REF result. On Windows PORT offers only http/1.1 by ALPN without an HTTP/2 option, as the Schannel reference does, so the TLS form is compared with `--http2-prior-knowledge`.
- REF, both forms: exit 0, stdout `[0 000 1]\nhello h2\n[2 200 0]\n`, stderr `curl: (92) HTTP/2 stream 1 was not closed cleanly: INTERNAL_ERROR (err 2)`. Wire: one connection. Preface, SETTINGS, WINDOW_UPDATE, HEADERS on 1, two WINDOW_UPDATEs on 1 after the reset, SETTINGS ack, HEADERS on 3, two WINDOW_UPDATEs on 3, GOAWAY `shutdown`. `-v` prints `Connection #0 to host H:P left intact` after the failure, then `Reusing existing http: connection`.
- PORT before the fix: exit 92, stdout `[0 000 1]\n[0 000 1]\n`, two connections, each ending in GOAWAY `shutdown` after stream 1 was reset. After the fix, both forms: exit, stdout, stderr and the frame sequence match REF. The only difference is the HEADERS on 1, 34 bytes against 36, because the `:authority` is 127.0.0.1 against the WSL host address 172.26.96.1.
- Fix: a peer's RST_STREAM now marks the transfer failure `HttpTransferException.LeavesConnectionIntact`, and `HttpProtocolHandler.FailedOutcome` keeps such a connection alive. It is marked reusable only while the session still takes new requests, and `-v` says `left intact`. This matches curl's multi_done, which keeps a multiplexed connection after a premature end. REFUSED_STREAM and HTTP_1_1_REQUIRED keep their own paths (Cgr032Tests pass). No ADR: this matches measured curl and makes no design choice.
- touches widened to HttpTransferException.cs and HttpProtocolHandler.cs, which the outcome decision lives in. No task in Doing on origin/work/dark-factory names either file.
- Cgr140Tests fails with the FailedOutcome line reverted and passes with it. Fast tests: 44200 passed, 0 failed. Measure-CodeQuality -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. The change is platform-neutral (no paths, OS text or certificates in the test), so CI on Linux and macOS is covered.

## Log

- 2026-10-26: Created from CGR-140 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. After a server RST_STREAM the HTTP/2 connection stays open and the next URL reuses it, as real curl does
