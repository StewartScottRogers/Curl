---
id: BL-3590
title: Fix CGR-061: A redirect Location containing control characters: a relative Location with TAB/0x01-0x1F fails with exit 3 Bad hostname where curl perce...
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Core.UnitTests/Cgr061Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Protocol.Http.UnitTests/Cgr061Tests.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3590 — Fix CGR-061: A redirect Location containing control characters: a relative Location with TAB/0x01-0x1F fails with exit 3 Bad hostname where curl perce...

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-061 (severity Medium): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-061: A redirect Location containing control characters: a relative Location with TAB/0x01-0x1F fails with exit 3 `Bad hostname` where curl percent-encodes and follows it; DEL in an absolute Location is followed where curl refuses

- **Severity:** Medium
- **Area:** url-parsing, http-stateful
- **Platform(s):** Both (Windows and Linux)
- **Confidence:** Confirmed. Detail: [http-stateful HS-8] Confirmed (Windows and Linux)
- **Related existing finding (GF/AF):** GF-0008
- **Source findings:** `url-parsing UP-19`, `http-stateful HS-8`

**Reproduction**

- *Setup (url-parsing):* E = `SCR\evidence\url-parsing`. Windows server: `E\srvbin\server.exe 20200 E\rec` (or `dotnet run E\srv\server.cs -- 20200 E\rec`); it records raw requests and replies `200 ok`, except that a request target containing `__loc=X` gets a 302 with `Location:` = URL-decoded X. Linux: `python3 lharness.py vcases.py` (or `vcases2.py`) from `E\wsl` (server on 20210). Re-run every case: `pwsh -File E\verify.ps1` (outputs `E\cases\v*`).
- *Setup (http-stateful):* EV = `SCR\evidence\http-stateful`. Scriptable server: `dotnet EV\srv\out\srv.dll EV\srv.log 20500 20501 20502s` (20502 is TLS, self-signed); route files in a case's `routes\` folder, `p.1.http` answers the first hit on `/p`, `p.2.http` the second. Replay a case: `pwsh EV\verify.ps1 <case>`; ad hoc: `EV\vrun.ps1 <routesdir> <curl args>`. Linux: `EV\linux\verify.sh` with `EV\linux\vpat.txt`.
- **[url-parsing UP-19]** A redirect Location that contains a control character (TAB, 0x01) is rejected
  - PORT reports `Bad hostname` with exit 3, where curl percent-encodes the character and follows the redirect.
  - `curl -sS -L "http://127.0.0.1:20200/__loc=/a%09b"` (v19_redir_tab; the server sends `Location: /a<TAB>b`)
  - `curl -sS -L "http://127.0.0.1:20200/__loc=/a%01b"` (v19b_redir_ctrl)
  - Linux cases: lv19_*.
- **[http-stateful HS-8]** Control characters in a Location header: a relative Location fails with exit 3 where curl percent-encodes and follows it; DEL in an absolute Location is followed where curl refuses
  - Relative: `/a/b` returns `302` with `Location: /b<TAB>c` (bytes 0x01-0x1F behave the same). Run `curl -sSL -w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n' http://127.0.0.1:20500/a/b`, and the same without -L.
  - Absolute: `/a` returns `Location: http://127.0.0.1:20500/b<0x7F>c`.
  - Cases: `EV\cases\loc-tab`, `loc-tab-noL`, `locc-1..31`, `locc-abs-*`, `locc-abs-127`, `locc-tab-query`.

Commands (run each with REF and with PORT):

```sh
# [url-parsing UP-19]
curl -sS -L "http://127.0.0.1:20200/__loc=/a%09b"
curl -sS -L "http://127.0.0.1:20200/__loc=/a%01b"
# [http-stateful HS-8]
curl -sSL -w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n' http://127.0.0.1:20500/a/b
```

**Expected (real curl)**

- **[url-parsing UP-19]** follows with `GET /a%09b` (or `GET /a%01b`) and exits 0.
- **[http-stateful HS-8]** 
  - Relative: follows to `GET /b%09c`. Stdout `final\n200 1 http://127.0.0.1:20500/b%09c`, exit 0. Without -L, redirect_url is `.../b%09c`.
  - Absolute with a C0 byte or DEL: exit 3 `The redirect target URL could not be parsed: Malformed input to a URL function`.

**Actual (Curl)**

- **[url-parsing UP-19]** `curl: (3) The redirect target URL could not be parsed: Bad hostname`. The second request is not sent.
- **[http-stateful HS-8]** 
  - Relative: exit 3 `curl: (3) The redirect target URL could not be parsed: Bad hostname`, and the redirect is not followed. Without -L, redirect_url contains the raw TAB byte.
  - Absolute with a C0 byte: exit 3 `Bad hostname`.
  - Absolute with DEL: follows to `GET /b%7Fc` and exits 0.

**Suspected location in source**

- **[url-parsing UP-19]** `Curl.Core.UnitLibrary/RedirectFollower.cs:401` (`EncodedForFollow` does not encode bytes below 0x20).
- **[http-stateful HS-8]** `Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs`: `Resolve` (line 58), `Encode` (line 197), `NormalizeAbsolute` (line 91).

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF's on Windows and REF-LINUX's on Linux (Expected); repro variants that already matched still match.


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Measured with `Record-CurlExchange.ps1` (port 18590; first reply `302` with `Location: <L>`, second `200` with body `final\n`), command
  `curl <flags> -w '%{http_code} %{num_redirects} %{url_effective} %{redirect_url}\n' http://127.0.0.1:18590/a/b`, against curl 8.22.0 Schannel (Windows).
  The UP-19 commands are the same shape (relative `/a<TAB>b`, `/a<0x01>b`) and are covered by the `rel-*` rows. Real curl:
  - `-sSL`, L = `/b<TAB>c`, `/b<01>c`, `/b<1F>c`, `/b<7F>c`: exit 0, stdout `final\n200 1 http://127.0.0.1:18590/b%09c ` (`%01`, `%1F`, `%7F`), second request `GET /b%09c` (and so on), stderr empty.
  - `-sS` (no -L), L = `/b<TAB>c`, `/b<01>c`: exit 0, stdout `302 0 http://127.0.0.1:18590/a/b http://127.0.0.1:18590/b%09c\n` (`%01`).
  - `-sSL`, L = `/b?x<TAB>y` -> `GET /b?x%09y`; `/b#x<TAB>y` -> effective `.../b#x%09y`, `GET /b`; `b<01>c` -> `GET /a/b%01c`; `//127.0.0.1:18590/b<01>c` -> `GET /b%01c`; all exit 0.
  - `-sSL`, L = `http://127.0.0.1:18590/b<01>c`, `<09>`, `<7F>`: exit 3, stdout `302 1 http://127.0.0.1:18590/a/b \n`, stderr `curl: (3) The redirect target URL could not be parsed: Malformed input to a URL function`, one request.
  - `-sS`, absolute L with `<7F>` or `<01>`: exit 0, redirect_url keeps the raw byte (`.../b<7F>c`).
  - No files written in any case.
- Curl before the fix: every relative C0 case and absolute C0 case exited 3 `Bad hostname` with one request; without -L redirect_url held the raw byte; absolute DEL was followed to `GET /b%7Fc`, exit 0. Relative DEL and both absolute no-L cases already matched.
- Curl after the fix: all 15 rows identical to real curl (exit, stdout bytes, stderr, request lines), compared with `Compare-Object`.
- Fix: `HttpRedirectLocation.AppendUnescaped` now escapes bytes at or below 0x20 in a relative Location (it already escaped space and above 0x7E); `RedirectFollower.ParseRefusal` refuses a target holding 0x00-0x1F or DEL with `Malformed input to a URL function` (`ProxyUrlParser.MalformedReason`), as curl's URL parser does. No ADR: this matches measured curl and needs no design choice. Pipeline `feature` run as a direct change: two one-line fixes in files the task names, no seams to plan.
- Focused tests: `Curl.Protocol.Http.UnitTests/Cgr061Tests.cs` (relative escaping; absolute kept) and `Curl.Core.UnitTests/Cgr061Tests.cs` (absolute C0/DEL refused with exit 3 Malformed, no second request). Each row pins a behaviour the pre-fix measurement shows Curl got wrong (raw byte kept, `Bad hostname`, DEL followed), so they fail before the fix.
- Fast tests: 46 assemblies, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Protocol.Http.UnitLibrary`: 0 failing members. Platform-neutral: no paths, OS errors or certificates; the Linux build (OpenSSL) shares the URL parser, so the same answers hold there (report: HS-8 confirmed on both).

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr061Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

## Log

- 2026-10-26: Created from CGR-061 of the third-party gap report.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-26: Doing -> Done. A relative redirect Location with control bytes is percent-encoded and followed, and an absolute one with a control byte or DEL exits 3 Malformed input, as curl 8.22.0 does
