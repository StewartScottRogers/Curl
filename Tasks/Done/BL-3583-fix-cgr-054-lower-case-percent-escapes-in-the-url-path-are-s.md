---
id: BL-3583
title: Fix CGR-054: Lower-case percent-escapes in the URL path are sent as typed; curl 8.22.0 upper-cases them (/a%2fb becomes /a%2Fb), which also changes AW...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartText.cs, Curl.Engine.UnitTests/Cgr054Tests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitTests/Cgr054Tests.cs, Curl.Protocol.Abstractions.UnitLibrary/UrlPathEscapes.cs, Curl.Protocol.Http.UnitLibrary/HttpUrlText.cs, Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs, Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Http.UnitTests/Cgr054Tests.cs, Curl.Protocol.Ws.UnitLibrary/WsUpgradeRequestFormatter.cs, Curl.Protocol.Ws.UnitTests/Cgr054Tests.cs, Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3583 — Fix CGR-054: Lower-case percent-escapes in the URL path are sent as typed; curl 8.22.0 upper-cases them (/a%2fb becomes /a%2Fb), which also changes AW...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-054 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-054: Lower-case percent-escapes in the URL path are sent as typed; curl 8.22.0 upper-cases them (`/a%2fb` becomes `/a%2Fb`), which also changes AWS SigV4 signatures

- **Severity:** Medium
- **Area:** url-parsing, config-glob-output, http-requests, other-protocols, http-stateful
- **Platform(s):** Windows. Detail: [url-parsing UP-05] Windows. Linux REF 8.18 also sends lower case, so this is 8.22 behaviour.; [config-glob-output CGO-03] Windows (the 8.22 target). Linux REF 8.18 does not upper-case either, so this is newer curl behaviour.; [http-requests HR-5] Windows (compared with REF 8.22.0; REF-LINUX 8.18.0 also leaves the path as typed); [other-protocols OP-12] Windows (against 8.22). REF-LINUX 8.18 also keeps lower case (`GET /a%2fb`), so this changed in curl after 8.18; PORT behaves the same on both platforms.; [http-stateful HS-18] Windows (Linux REF 8.18 is older than this change)
- **Confidence:** Confirmed. Detail: [url-parsing UP-05] Confirmed on Windows against 8.22. On Linux, PORT and REF 8.18 match.; [config-glob-output CGO-03] Confirmed on Windows.; [http-requests HR-5] Confirmed: reproduced 2/2 on Windows.; [other-protocols OP-12] Confirmed (Windows).
- **Related existing finding (GF/AF):** GF-0024
- **Source findings:** `url-parsing UP-05`, `config-glob-output CGO-03`, `http-requests HR-5`, `other-protocols OP-12`, `http-stateful HS-18`

REF-LINUX 8.18.0 also leaves the path as typed, so this is curl 8.22 behaviour. The query string is left alone by both. `-O` file names keep the typed form in both.

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (config-glob-output):* E = `SCR\evidence\config-glob-output`. Windows server: `dotnet run E\srv\server.cs -- 20102 E\verify\log.txt` (listens on 127.0.0.1 and [::1], logs raw requests, replies `200 OK` with body `path=<request target>`; query keys `lm=` adds Last-Modified, `cd=`/`cdhex=` add Content-Disposition, `size=n` body of n `A`, `short=n` over-declares Content-Length, `status=n`). Linux: `python3 E/linux/server.py 20103 log`. `$U` = `http://127.0.0.1:20102` (20103 on Linux). Run each case in a fresh directory with HOME, USERPROFILE and APPDATA pointing at it and CURL_HOME/XDG_CONFIG_HOME unset. Verifier scripts: `E\verify\v1_stdin.ps1`, `v2_misc.ps1`, `v3_misc.ps1`, `v4_maxpath.ps1`, `E\verify\linux\vl.sh`.
- *Setup (http-requests):* E = `SCR\evidence\http-requests`. Recording loopback server + differential runner: `dotnet run E\diff.cs -- E\verify.json E\vout1 20335` (Windows, port 20335) and `E/vlinux.sh` (WSL, port 20355; runs `python3 -I E/diff.py E/verify.json ~/hr_verify/v1 20355`). The server answers `200 OK` with body `ok\n` after reading the whole request; in `early` mode it answers right after the request head and keeps reading.
- *Setup (other-protocols):* EV = `SCR\evidence\other-protocols`. Scripted per-protocol loopback servers and runner in `EV\bench\Program.cs`; Windows `pwsh -File EV\run.ps1 <file>.cases [filter]`, Linux `bash EV/run.sh <file>.cases` (set `FILTER=`) or `EV/verify-linux.sh [filter]`. Each `.cases` line is `name | server-mode | server options | curl args`. Default servers: SMTP answers 250 and offers AUTH PLAIN LOGIN CRAM-MD5 XOAUTH2 OAUTHBEARER; POP3 offers SASL PLAIN LOGIN CRAM-MD5; IMAP offers AUTH=PLAIN AUTH=LOGIN AUTH=CRAM-MD5 SASL-IR.
- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- **[url-parsing UP-05]** Lower-case percent escapes in the URL path are not upper-cased on the wire
  - `curl -sS "http://127.0.0.1:20200/a%2fb%7e"` (v05_path_lowerhex)
  - `curl -sS --path-as-is "http://127.0.0.1:20200/a/%2e%2e/b"` (v05b_pai_lower)
- **[config-glob-output CGO-03]** The port keeps percent-encoded hex digits in the URL path as typed; curl 8.22 sends them in upper case
  - `curl -sS "$U/%7euser/%e2%82%ac"`
  - `curl -sS -O "$U/%2e%2e%2fevil"`
  - Script: `E\verify\v2_misc.ps1` p1/p2. The originals are `E\b_out0.ps1` (o09, o10, o42) and `E\b_misc.ps1` (s18, s19).
- **[http-requests HR-5]** Lower-case percent escapes in the URL path are not upper-cased (curl 8.22.0 sends /a%2fb as /a%2Fb)
  - Run `curl -sS http://127.0.0.1:20335/a%2fb`, case v5_pct_lower. The finder's cases are in `E\c10.json`.
- **[other-protocols OP-12]** WebSocket and HTTP request paths keep lower-case percent-escapes where curl 8.22 upper-cases them
  - `EV\pct.cases` pc01/pc02 and `EV\confirm.cases` C18. Commands:
    - `curl -s -S "ws://127.0.0.1:21001/a%2fb%3d"`
    - `curl -s -S "http://127.0.0.1:21001/a%0d%0ab%2fc"`
- **[http-stateful HS-18]** Lower-case percent-escapes in the URL path are sent as given; curl 8.22 upper-cases them, which also changes SigV4 signatures
  - `curl -sS 'http://127.0.0.1:20500/a%2fb%c3%bc?x=%2f'`, and `curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H 'X-Amz-Date: 20250101T000000Z' http://127.0.0.1:20500/a%2fb%c3%bc`. Cases: `EV\cases\pct-lower-plain`, `svx-lower-v`.

Commands (run each with REF and with PORT):

```sh
# [url-parsing UP-05]
curl -sS "http://127.0.0.1:20200/a%2fb%7e"
curl -sS --path-as-is "http://127.0.0.1:20200/a/%2e%2e/b"
# [config-glob-output CGO-03]
curl -sS "$U/%7euser/%e2%82%ac"
curl -sS -O "$U/%2e%2e%2fevil"
E\verify\v2_misc.ps1
E\b_out0.ps1
E\b_misc.ps1
# [http-requests HR-5]
curl -sS http://127.0.0.1:20335/a%2fb
# [other-protocols OP-12]
curl -s -S "ws://127.0.0.1:21001/a%2fb%3d"
curl -s -S "http://127.0.0.1:21001/a%0d%0ab%2fc"
# [http-stateful HS-18]
curl -sS 'http://127.0.0.1:20500/a%2fb%c3%bc?x=%2f'
curl -sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H 'X-Amz-Date: 20250101T000000Z' http://127.0.0.1:20500/a%2fb%c3%bc
```

**Expected (real curl)**

- **[url-parsing UP-05]** `GET /a%2Fb%7E` and `GET /a/%2E%2E/b`. `%{url_effective}` keeps the lower case.
- **[config-glob-output CGO-03]** 
  - Sends `GET /%7Euser/%E2%82%AC HTTP/1.1`, and likewise `GET /%2E%2E%2Fevil`.
  - The `-O` file name keeps the typed form `%2e%2e%2fevil`.
- **[http-requests HR-5]** Sends `GET /a%2Fb HTTP/1.1`. Lower-case escapes in the query are left alone, and the port matches that (control case v5_pct_query).
- **[other-protocols OP-12]** `GET /a%2Fb%3D HTTP/1.1` and `GET /a%0D%0Ab%2Fc HTTP/1.1`
- **[http-stateful HS-18]** sends `GET /a%2Fb%C3%BC?x=%2f`. The path is upper-cased and the query is left alone. Signature `de9fa8b6...`.

**Actual (Curl)**

- **[url-parsing UP-05]** `GET /a%2fb%7e` and `GET /a/%2e%2e/b`.
- **[config-glob-output CGO-03]** 
  - Sends `GET /%7euser/%e2%82%ac HTTP/1.1` unchanged.
  - The request line therefore differs, and so does any server output that echoes the path.
  - The `-O` file name matches REF.
- **[http-requests HR-5]** Sends `GET /a%2fb HTTP/1.1`.
- **[other-protocols OP-12]** `GET /a%2fb%3d HTTP/1.1` and `GET /a%0d%0ab%2fc HTTP/1.1`. The query string, gopher, RTSP, IMAP, SMTP, DICT and MQTT paths match.
- **[http-stateful HS-18]** sends `GET /a%2fb%c3%bc?x=%2f`. Signature `ec301c3d...`.

**Suspected location in source**

- **[url-parsing UP-05]** `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs` (Build, around :271).
- **[config-glob-output CGO-03]** the port's URL path normalisation (not located).
- **[http-requests HR-5]** `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs:289-293`. The path gets dot-segment removal only, with no escape upper-casing. Compare `Curl.Engine.UnitLibrary/UrlPartText.cs`, which does this for redirect URLs.
- **[other-protocols OP-12]** URL path normalisation in the URL parser; not specific to these protocols.
- **[http-stateful HS-18]** the CurlUrl path normalisation in `Curl.Protocol.Abstractions.UnitLibrary`. Overlaps the url-parsing area.

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr054Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- Touches widened (BL-3583 run): the fix cannot sit in `CurlUrlParser` because `CurlUrl.AbsolutePath` feeds `%{url_effective}` (which curl keeps lower case) and the proxy absolute form (measured as typed), so it went to the request-target builders: new `Curl.Protocol.Abstractions.UnitLibrary/UrlPathEscapes.cs`, `HttpUrlText.cs` (`OriginTarget`), `HttpRequestHeadFormatter.cs`, `HttpProtocolHandler.cs` (auth target), `WsUpgradeRequestFormatter.cs`, `AwsSigV4HttpAuthenticator.cs`, and a `Cgr054Tests.cs` in the Http and Ws test projects. No task in Doing on `origin/work/dark-factory` named any of them. `CurlUrlParser.cs` and `UrlPartText.cs` were not changed. Decision recorded in ADR-0821.
- Measured 2026-10-26, `Record-CurlExchange.ps1` on port 20583, REF = Git for Windows ucrt64 curl 8.22.0 (Schannel), PORT = this branch's `Curl.Console\bin\Debug\net10.0\curl.exe`. Exit code, stdout, stderr and request line (plus `Authorization` for SigV4) were identical for all 12 commands:
  1. `-sS http://P/a%2fb%7e` -> `GET /a%2Fb%7E`, exit 0
  2. `-sS --path-as-is http://P/a/%2e%2e/b` -> `GET /a/%2E%2E/b`
  3. `-sS http://P/%7euser/%e2%82%ac` -> `GET /%7Euser/%E2%82%AC`
  4. `-sS "http://P/a%2fb%c3%bc?x=%2f"` -> `GET /a%2Fb%C3%BC?x=%2f` (query as typed)
  5. `-sS http://P/a%0d%0ab%2fc` -> `GET /a%0D%0Ab%2Fc`
  6. `-sS http://P/a/%2e%2e/b` -> `GET /b` (already matched)
  7. `-sS http://P/a%zz%2/b%a` -> `GET /a%zz%2/b%a`
  8. `-sS -w %{url_effective} http://P/a%2fb` -> `GET /a%2Fb`, stdout `http://127.0.0.1:20583/a%2fb` (already matched)
  9. `-sS --aws-sigv4 aws:amz:us-east-1:s3 -u AKID:SECRET -H "X-Amz-Date: 20250101T000000Z" http://P/a%2fb%c3%bc` -> `GET /a%2Fb%C3%BC`, `Signature=c12822d857213145443eee04286c029d0057b90da2e0abf9acfd15b23cc00332` (the report's `de9fa8b6...` was signed for its own host and port)
  10. `-sS -x http://P http://example.com/a%2fb` -> `GET http://example.com/a%2fb` (proxy absolute form as typed; already matched)
  11. `-sS ws://P/a%2fb%3d` -> `GET /a%2Fb%3D`, exit 22 `curl: (22) Refused WebSocket upgrade: 200`
  12. `-sS -p -x http://P http://example.com/a%2fb` -> exit 56 `curl: (56) Recv failure: Connection was aborted` (already matched)
  `-O` file names come from the URL text and are unchanged (the report shows they already matched).
- Found while measuring: on a Digest 401 retry curl 8.22.0 also upper-cases the query (`GET /a%2Fb?q=%2F`), which is not one of this issue's repros; filed as BL-3849.
- Quality (`Measure-CodeQuality.ps1` over Abstractions, Http, Ws and Engine): Abstractions, Http and Ws 100%/100%; Engine's one failing member is BL-3562's `MultiCallbackQueue.QueuedCallback.WaitUntilRun` (a timing-dependent branch this task did not touch), filed as BL-3850. Every member this task changed passes.
- Fails before the fix: before it the request line, upgrade target and SigV4 path carried the typed lower-case escapes (measured above as the report's Actual), so every `Cgr054Tests` assertion on upper case fails there; `UrlPathEscapes` and `HttpUrlText.OriginTarget` did not exist.
- CI on Linux and macOS: the new tests are pure string computation with no path, OS or certificate, so they are platform-neutral; fast tests green here (exit 0).

## Log

- 2026-10-26: Created from CGR-054 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. Lower-case percent-escapes in the URL path go out upper-cased in the request line, WebSocket upgrade and SigV4 signature, matching curl 8.22.0 on every CGR-054 repro
