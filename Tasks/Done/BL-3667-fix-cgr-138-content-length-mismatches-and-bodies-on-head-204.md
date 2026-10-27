---
id: BL-3667
title: Fix CGR-138: Content-Length mismatches, and bodies on HEAD, 204 or 1xx responses, are not HTTP/2 protocol errors in PORT; REF exits 16 even with --ign...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitLibrary/HttpH2cUpgradeConnection.cs, Curl.Protocol.Http.UnitTests/Cgr138Tests.cs, Curl.Protocol.Http.UnitTests/Http2SessionTests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3667 — Fix CGR-138: Content-Length mismatches, and bodies on HEAD, 204 or 1xx responses, are not HTTP/2 protocol errors in PORT; REF exits 16 even with --ign...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-138 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-138: Content-Length mismatches, and bodies on HEAD, 204 or 1xx responses, are not HTTP/2 protocol errors in PORT; REF exits 16 even with --ignore-content-length

- **Severity:** Medium (PORT exits 0 with a truncated body for `cllong`)
- **Area:** http2-linux
- **Platform(s):** Both (Windows and Linux). Detail: Both (PORT-WIN matches: `EV\win\r_cllong`, `EV\win\r_204body`)
- **Confidence:** Confirmed. Detail: Confirmed (RFC 9113 8.1.1 check in nghttp2; reproduced by the verifier and on PORT-WIN)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-7`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Server scens:
  - `cllong`: content-length 3, 9 data bytes.
  - `clshort`: content-length 100, 9 bytes.
  - `204body`: 204 followed by DATA.
  - `1xxes`: 103 with END_STREAM.
  - `clbad`: content-length `abc`.
  - `cltwodiff`: two different content-length values.
- Commands:
  - `curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x`
  - `curl -sS --http2-prior-knowledge --ignore-content-length http://127.0.0.1:P/x` (scen `cllong`)
  - `curl -sS --http2-prior-knowledge -X HEAD -m 5 http://127.0.0.1:P/x` (scen `ok` sends a body)
- Cases: `r_cllong`, `r_clshort`, `r_204body`, `r_1xxes`, `r2_clbad`, `r2_cltwodiff`, `z_icl_long`, `z_X_HEAD`.

Commands (run each with REF and with PORT):

```sh
curl -sS --http2-prior-knowledge -i http://127.0.0.1:P/x
curl -sS --http2-prior-knowledge --ignore-content-length http://127.0.0.1:P/x
curl -sS --http2-prior-knowledge -X HEAD -m 5 http://127.0.0.1:P/x
```

**Expected (real curl)**

- Exit 16 with `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR` for `cllong`, `clshort`, `204body`, `1xxes`, `-X HEAD` and `--ignore-content-length`.
- `clbad` and `cltwodiff` exit 16 with `Invalid HTTP header field was received ... name: [content-length]`.
- In every case REF sends GOAWAY PROTOCOL_ERROR.

**Actual (Curl)**

| Case | Exit | Output |
|---|---|---|
| `cllong` | 0 | writes only `hel` |
| `clshort` | 18 | `curl: (18) end of response with 91 bytes missing` |
| `204body` | 0 | |
| `1xxes` | 52 | `curl: (52) Empty reply from server` |
| `clbad`, `cltwodiff` | 8 | `curl: (8) Invalid Content-Length: value` |
| `-X HEAD` | 0 | writes `hello h2` |
| `--ignore-content-length` | 0 | writes `hello h2` |

**Suspected location in source**

- `Curl.Protocol.Http.UnitLibrary\Http2StreamConnection.cs` (no RFC 9113 8.1.1 check of content-length against the DATA received)

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2), for both PORT-LINUX and PORT-WIN (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr138Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.
- Measured (2026-10-26): real curl 8.18.0 (WSL, nghttp2 1.68.0) and Curl (`dotnet run --project Curl.Console`) against one scripted HTTP/2 server, a C# file-based app outside the repository (prior knowledge: SETTINGS, ACK, then per scenario a HEADERS of literal fields and one DATA `hello h2\n` with END_STREAM). Record-CurlExchange's `-Http2Origin` answers only `content-length: 0`, so it could not serve these bodies. Commands: `curl -sS --http2-prior-knowledge -i http://HOST:P/x` (cllong, clshort, 204body, 1xxes, clbad, cltwodiff, ok, and 304body added here), `curl -sS --http2-prior-knowledge --ignore-content-length ...` (cllong), `curl -sS --http2-prior-knowledge -X HEAD -m 5 ...` (ok).
- REF results: every malformed case exits 16 and sends GOAWAY `0000000000000001` (last stream 0, PROTOCOL_ERROR, no debug data) and no RST_STREAM. stderr is `curl: (16) nghttp2 shuts down connection with error 1: PROTOCOL_ERROR`, except clbad and cltwodiff: `curl: (16) Invalid HTTP header field was received: frame type: 1, stream: 1, name: [content-length], value: [abc]` (and `[10]`). stdout with -i: cllong `HTTP/2 200 \r\ncontent-length: 3\r\n\r\n`, so none of the DATA; clshort the head plus `hello h2\n`; 204body `HTTP/2 204 \r\n\r\n`; 304body `HTTP/2 304 \r\n\r\n`; 1xxes `HTTP/2 103 \r\nlink: </s.css>\r\n`, with no empty line; clbad `HTTP/2 200 \r\n`; cltwodiff `HTTP/2 200 \r\ncontent-length: 9\r\n`; --ignore-content-length and -X HEAD write nothing. ok exits 0 with the head and body, and its closing GOAWAY carries `shutdown\0`.
- Before the fix Curl gave the issue's Actual table. After it, Curl's exit code, stdout bytes, stderr text and frames sent equal REF's for all 9 cases. The HEADERS block is compared only after normalising host and user-agent, and stderr differs only by CRLF against LF, which is Windows against Linux line endings and normalised by the report's section 1. No case writes a file.
- Fix (ADR-0843): `Http2StreamConnection` tracks the body length the head promises (0 for HEAD, 204 and 304), and checks content-length fields as nghttp2 does: digits only, at most one. A failure sends GOAWAY at once and is thrown by the next read, after the bytes curl writes before it.
- The full fast run then failed upstream test1702 (HEAD over an h2c upgrade): the upgraded stream did not know its request was a HEAD. So `HttpH2cUpgradeConnection.cs` now sees the first request head written and passes `isHead` to `StartUpgradedAsync`. I added it, and `Http2SessionTests.cs` (one call site of the changed signature), to `touches`; no task in Doing on origin/work/dark-factory names either file.
- Cgr138Tests: 10 malformed rows, 9 of which failed with the original `Http2StreamConnection.cs` and pass now (the 304 row was added after that check), plus 2 rows whose lengths add up and an h2c upgrade test (-I passes, GET exits 16). Fast tests green (exit 0). Measure-CodeQuality for Curl.Protocol.Http.UnitLibrary: 100% line, 100% branch, worst CRAP 10, 0 failing members. CI on Linux and macOS: the tests use no paths, files or OS text, so they are platform-neutral.

## Log

- 2026-10-26: Created from CGR-138 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. HTTP/2 responses whose length does not add up, bodies on HEAD/204/304, 1xx ending the stream and invalid content-length exit 16 with GOAWAY PROTOCOL_ERROR, matching curl 8.18.0 with nghttp2
